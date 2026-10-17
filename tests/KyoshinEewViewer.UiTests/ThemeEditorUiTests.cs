using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinEewViewer.Views;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests;

public sealed class ThemeEditorUiTests
{
	[AvaloniaFact]
	public async Task テーマ名の入力は下書きとプレビューへ反映され組み込みテーマを変更しない()
	{
		var selector = ThemeSelector.Create(null);
		var previousSelector = KyoshinEewViewerApp.Selector;
		KyoshinEewViewerApp.Selector = selector;
		var original = selector.SelectedWindowTheme!;
		var name = original.Theme.Name;
		var window = new WindowThemeEditWindow { WindowTheme = original };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var draft = Assert.IsType<WindowTheme>(window.DataContext);
			Assert.NotSame(original.Theme, draft);
			var editor = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Text == name);
			Assert.True(editor.Focus());
			window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
			window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
			window.KeyTextInput("UIテスト用テーマ");
			Assert.Equal("UIテスト用テーマ", draft.Name);
			await Ui.WaitUntilAsync(() => selector.SelectedWindowTheme?.Theme.Name == draft.Name, "編集中のテーマがプレビューへ反映される");
			Assert.Equal(name, original.Theme.Name);
		}
		finally
		{
			// このケースの対象外である未保存終了のダイアログを、後処理では開かない。
			window.WindowTheme = null;
			window.Close();
			Dispatcher.UIThread.RunJobs();
			KyoshinEewViewerApp.Selector = previousSelector;
		}
	}

	[AvaloniaFact]
	public Task 未保存の編集は閉じる前に確認し_いいえなら編集を続け_はいなら閉じて元のテーマに戻す()
		=> ForEachEditor(async session =>
		{
			await session.EditNameAsync("UIテスト用テーマ");

			session.Window.Close();
			var dialog = await WaitDialogAsync(session.Window, "テーマの変更を破棄");
			Assert.Contains("ウィンドウを閉じて編集中のテーマの変更を破棄しますか？", Ui.VisibleTexts(dialog));
			Ui.Click(session.Window, Ui.FindButton(dialog, "いいえ"));
			await WaitDialogClosedAsync(session.Window);
			Assert.False(session.IsClosed());
			Assert.Equal("UIテスト用テーマ", session.Editor.DraftName(session.Window));

			session.Window.Close();
			dialog = await WaitDialogAsync(session.Window, "テーマの変更を破棄");
			Ui.Click(session.Window, Ui.FindButton(dialog, "はい"));
			await Ui.WaitUntilAsync(
				() => session.IsClosed() && ReferenceEquals(session.Editor.Selected(session.Selector), session.Original),
				"画面が閉じて元のテーマに戻る");
			Assert.Equal(session.OriginalName, session.Editor.PreviewName(session.Selector));
		});

	[AvaloniaFact]
	public Task 復元すると編集中の変更を破棄して元のテーマに戻り_閉じるときに確認しない()
		=> ForEachEditor(async session =>
		{
			await session.EditNameAsync("UIテスト用テーマ");

			Ui.Click(session.Window, session.Window.FindControl<Button>("rollbackButton")!);
			var dialog = await WaitDialogAsync(session.Window, "復元する");
			Assert.Contains("編集中のテーマの変更を破棄しますか？", Ui.VisibleTexts(dialog));
			Ui.Click(session.Window, Ui.FindButton(dialog, "はい"));
			await Ui.WaitUntilAsync(
				() => session.Editor.DraftName(session.Window) == session.OriginalName
					&& session.Editor.PreviewName(session.Selector) == session.OriginalName,
				"下書きとプレビューが元のテーマ名に戻る");
			await WaitDialogClosedAsync(session.Window);

			session.Window.Close();
			await Ui.WaitUntilAsync(
				() => session.IsClosed() && ReferenceEquals(session.Editor.Selected(session.Selector), session.Original),
				"確認なしで画面が閉じて元のテーマに戻る");
		});

	/// <summary>
	/// ウィンドウテーマと震度テーマの編集画面は同じ構造のため、同じ操作で検証する
	/// </summary>
	private sealed record EditorCase(
		string Name,
		Func<ThemeSelector, Window> Open,
		Func<ThemeSelector, object?> Selected,
		Func<ThemeSelector, string?> PreviewName,
		Func<Window, string?> DraftName,
		Action<Window> Detach);

	private static readonly EditorCase[] Editors =
	[
		new("ウィンドウテーマ",
			s => new WindowThemeEditWindow { WindowTheme = s.SelectedWindowTheme },
			s => s.SelectedWindowTheme,
			s => s.SelectedWindowTheme?.Theme.Name,
			w => (w.DataContext as WindowTheme)?.Name,
			w => ((WindowThemeEditWindow)w).WindowTheme = null),
		new("震度テーマ",
			s => new IntensityThemeEditWindow { IntensityTheme = s.SelectedIntensityTheme },
			s => s.SelectedIntensityTheme,
			s => s.SelectedIntensityTheme?.Theme.Name,
			w => (w.DataContext as IntensityTheme)?.Name,
			w => ((IntensityThemeEditWindow)w).IntensityTheme = null),
	];

	private sealed record EditorSession(EditorCase Editor, ThemeSelector Selector, Window Window, object Original, string OriginalName, Func<bool> IsClosed)
	{
		public async Task EditNameAsync(string name)
		{
			var textBox = Window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Text == OriginalName);
			Assert.True(textBox.Focus());
			Ui.PressKey(Window, PhysicalKey.A, RawInputModifiers.Control);
			Window.KeyTextInput(name);
			await Ui.WaitUntilAsync(() => Editor.PreviewName(Selector) == name, "編集中のテーマがプレビューへ反映される");
		}
	}

	private static async Task ForEachEditor(Func<EditorSession, Task> test)
	{
		foreach (var editor in Editors)
		{
			var selector = ThemeSelector.Create(null);
			var previousSelector = KyoshinEewViewerApp.Selector;
			KyoshinEewViewerApp.Selector = selector;
			var original = editor.Selected(selector)!;
			var window = editor.Open(selector);
			var closed = false;
			window.Closed += (_, _) => closed = true;
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();
				await test(new(editor, selector, window, original, editor.PreviewName(selector)!, () => closed));
			}
			catch (Exception ex)
			{
				throw new XunitException($"{editor.Name}の編集画面: {ex.Message}", ex);
			}
			finally
			{
				if (!closed)
				{
					editor.Detach(window);
					window.Close();
					Dispatcher.UIThread.RunJobs();
				}
				KyoshinEewViewerApp.Selector = previousSelector;
			}
		}
	}

	private static async Task<FAContentDialog> WaitDialogAsync(Window window, string title)
	{
		FAContentDialog? dialog = null;
		await Ui.WaitUntilAsync(() =>
		{
			dialog = window.GetVisualDescendants().OfType<FAContentDialog>().SingleOrDefault(d => Equals(d.Title, title));
			return dialog?.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible) == true;
		}, $"「{title}」ダイアログが表示される");
		return dialog!;
	}

	private static Task WaitDialogClosedAsync(Window window)
		=> Ui.WaitUntilAsync(() => !window.GetVisualDescendants().OfType<FAContentDialog>().Any(), "ダイアログが閉じる");
}
