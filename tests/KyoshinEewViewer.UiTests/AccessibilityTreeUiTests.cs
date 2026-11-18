using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.UiTests;

// macOS などのアクセシビリティ API は、画面の要素をたどるために AutomationPeer の子要素を問い合わせる。
// 問い合わせの途中で例外が発生すると、Avalonia.Native ではアプリケーションが終了する
public sealed class AccessibilityTreeUiTests
{
	[AvaloniaFact]
	public void 設定画面のアクセシビリティツリーをたどっても例外が発生せず_子ページは展開して表示される()
	{
		var config = new KyoshinEewViewerConfiguration();
		config.Update.Enable = false;

		var services = new ServiceCollection();
		services.AddSingleton(config);
		services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(_ => { }));
		services.AddKyoshinEewViewer();
		using var provider = services.BuildServiceProvider();
		// 設定画面は閉じるときに DI コンテナから設定を取得して保存する
		ServiceLocator.SetProvider(provider);

		var walk = new TreeWalk();
		var viewModel = provider.GetRequiredService<SettingWindowViewModel>();
		var window = new SettingWindow { DataContext = viewModel };
		try
		{
			walk.ShowAndWalk(window, "設定画面");
			Assert.Contains(window.GetVisualDescendants().OfType<FANavigationViewItem>(),
				item => item.IsEffectivelyVisible && item.GetVisualAncestors().OfType<FANavigationViewItem>().Any());

			foreach (var page in Flatten(viewModel.SettingPages).Where(p => p.IsVisible))
			{
				viewModel.SelectedSettingPage = page;
				Dispatcher.UIThread.RunJobs();
				walk.Walk(window, $"ページ「{page.Title}」");
			}

			// 子ページのない項目をクリックしても、子ページ用の FAItemsRepeater が表示されない
			var leaf = window.GetVisualDescendants().OfType<FANavigationViewItem>()
				.First(item => item.IsEffectivelyVisible && item.DataContext is ISettingPage { SubPages.Length: 0 });
			Ui.Click(window, leaf);
			Dispatcher.UIThread.RunJobs();
			Assert.False(leaf.IsExpanded);
			walk.Walk(window, "子ページのない項目をクリックした後");
		}
		finally
		{
			window.Close();
			Dispatcher.UIThread.RunJobs();
		}
		walk.AssertNoFailures();
	}

	// メイン画面は ViewModel の生成時に受信や更新確認を始めるため、ナビゲーションの設定だけを再現する
	[AvaloniaFact]
	public void メイン画面と同じ設定のナビゲーションのアクセシビリティツリーをたどっても例外が発生しない()
	{
		var walk = new TreeWalk();
		// シリーズを単独で起動したとき (-s) は LeftMinimal になる
		foreach (var mode in new[] { FANavigationViewPaneDisplayMode.Left, FANavigationViewPaneDisplayMode.LeftMinimal })
		{
			var window = new Window
			{
				Content = new FANavigationView
				{
					AlwaysShowHeader = false,
					IsSettingsVisible = false,
					PaneDisplayMode = mode,
					IsPaneOpen = true,
					IsBackEnabled = false,
					IsBackButtonVisible = false,
					MenuItemsSource = new[] { "強震モニタ", "地震情報", "津波情報" },
					MenuItemTemplate = new FuncDataTemplate<string>((name, _) => new FANavigationViewItem { Content = name }),
				},
			};
			try
			{
				walk.ShowAndWalk(window, mode.ToString());
			}
			finally
			{
				window.Close();
				Dispatcher.UIThread.RunJobs();
			}
		}
		walk.AssertNoFailures();
	}

	private static IEnumerable<ISettingPage> Flatten(IEnumerable<ISettingPage> pages)
		=> pages.SelectMany(p => Flatten(p.SubPages).Prepend(p));

	private sealed class TreeWalk
	{
		private readonly List<string> _failures = [];
		private readonly List<string> _emptyRepeaters = [];

		/// <summary>
		/// 表示した直後とレイアウトの後に、ウィンドウのアクセシビリティツリーをすべてたどる
		/// </summary>
		public void ShowAndWalk(Window window, string label)
		{
			window.Show();
			Walk(window, $"{label} 表示直後");
			Dispatcher.UIThread.RunJobs();
			Walk(window, $"{label} レイアウト後");
		}

		public void Walk(Window window, string label)
		{
			var failureCount = _failures.Count;
			Walk(ControlAutomationPeer.CreatePeerForElement(window), $"[{label}] {Describe(window)}");
			if (_failures.Count == failureCount)
				return;
			// 失敗したときの手がかりとして、子要素がないまま表示されている FAItemsRepeater を記録する
			_emptyRepeaters.AddRange(window.GetVisualDescendants().OfType<FAItemsRepeater>()
				.Where(r => r.IsEffectivelyVisible && !r.GetVisualChildren().Any())
				.Select(r => $"[{label}] " + string.Join(" > ", r.GetSelfAndVisualAncestors().Reverse().OfType<Control>()
					.Where(c => c.Name != null || c is FAItemsRepeater)
					.Select(Describe))));
		}

		public void AssertNoFailures()
			=> Assert.True(_failures.Count == 0, string.Join(Environment.NewLine, [
				"子要素の問い合わせで例外が発生しました:",
				.. _failures,
				"子要素がないまま表示されている FAItemsRepeater:",
				.. _emptyRepeaters,
			]));

		private void Walk(AutomationPeer peer, string path)
		{
			IReadOnlyList<AutomationPeer> children;
			try
			{
				children = peer.GetChildren();
			}
			catch (Exception ex)
			{
				_failures.Add($"{path}: {ex.GetType().Name}: {ex.Message}");
				return;
			}
			foreach (var child in children)
				Walk(child, child is ControlAutomationPeer c ? $"{path} > {Describe(c.Owner)}" : $"{path} > {child.GetType().Name}");
		}

		private static string Describe(Control control)
			=> control.Name is { } name ? $"{control.GetType().Name}#{name}" : control.GetType().Name;
	}
}
