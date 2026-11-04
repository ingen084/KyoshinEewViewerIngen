#if INTEGRATION_TEST
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 結合テストビルド専用: 実際のプラットフォーム上で画面を操作する
/// </summary>
internal static class UiOperations
{
	private const double TimeoutSeconds = 30;

	public static async Task<T> WaitForAsync<T>(Func<T?> find, string description) where T : class
	{
		var deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
		while (true)
		{
			if (find() is { } found)
				return found;
			if (DateTime.UtcNow >= deadline)
				throw new TimeoutException($"{TimeoutSeconds}秒以内に見つかりませんでした: {description}");
			await Task.Delay(50);
		}
	}

	public static Task WaitUntilAsync(Func<bool> condition, string description)
		=> WaitForAsync(() => condition() ? description : null, description);

	public static Task<T> WaitForControlAsync<T>(Visual root) where T : Visual
		=> WaitForAsync(() => root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.IsEffectivelyVisible), typeof(T).Name);

	public static Task WaitForTextAsync(Visual root, string text)
		=> WaitUntilAsync(() => VisibleTexts(root).Contains(text), $"テキスト「{text}」");

	public static Task WaitForTextStartingWithAsync(Visual root, string prefix)
		=> WaitUntilAsync(() => VisibleTexts(root).Any(t => t.StartsWith(prefix, StringComparison.Ordinal)), $"「{prefix}」で始まるテキスト");

	/// <summary>
	/// 表示されているテキストを列挙する
	/// </summary>
	public static string[] VisibleTexts(Visual root)
		=> [.. root.GetVisualDescendants().OfType<TextBlock>()
			.Where(t => t.IsEffectivelyVisible)
			.Select(t => string.IsNullOrEmpty(t.Text) ? t.Inlines?.Text : t.Text)
			.OfType<string>()];

	/// <summary>
	/// 折りたたまれた項目はバインディングが評価されないので、すべて開く
	/// </summary>
	public static async Task ExpandAllAsync(Visual root)
	{
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
		foreach (var expander in root.GetVisualDescendants().OfType<FASettingsExpander>())
			expander.IsExpanded = true;
		await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	/// <summary>
	/// ツールチップで指定したボタンが表示されるのを待って押す
	/// </summary>
	public static async Task ClickByTipAsync(Visual root, string tip)
		=> Click(await WaitForAsync(
			() => root.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == tip),
			$"ボタン「{tip}」"));

	/// <summary>
	/// 文字で指定したボタンが表示されるのを待って押す
	/// </summary>
	public static async Task ClickByContentAsync(Visual root, string content)
		=> Click(await WaitForAsync(
			() => root.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.IsEffectivelyVisible && Equals(b.Content, content)),
			$"ボタン「{content}」"));

	/// <summary>
	/// 同じ文字のボタンが複数あるときに、画面の並びの順番で指定して押す
	/// </summary>
	public static async Task ClickByContentAsync(Visual root, string content, int index)
		=> Click(await WaitForAsync(
			() => root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && Equals(b.Content, content)).ElementAtOrDefault(index),
			$"{index + 1}番目のボタン「{content}」"));

	/// <summary>
	/// ボタンを押す。無効なボタンは実際の操作と同じように押せず、例外になる
	/// </summary>
	private static void Click(Button button)
		=> new ButtonAutomationPeer(button).Invoke();

	/// <summary>
	/// ボタンのメニューを開き、表示された項目を確認してから1つ選ぶ
	/// </summary>
	public static async Task ChooseMenuItemAsync(Button button, IEnumerable<string> expectedItems, string item)
	{
		var items = await OpenMenuAsync(button, expectedItems);
		items[Array.FindIndex(items, i => i.Header as string == item)].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
		button.Flyout!.Hide();
	}

	/// <summary>
	/// ボタンのメニューを開き、表示された項目を確認する
	/// </summary>
	public static async Task<MenuItem[]> OpenMenuAsync(Button button, IEnumerable<string> expectedItems)
	{
		if (button.Flyout is not MenuFlyout flyout)
			throw new InvalidOperationException("ボタンにメニューがありません");

		Click(button);
		var items = await WaitForAsync(
			() => flyout.Popup.Child?.GetVisualDescendants().OfType<MenuItem>().ToArray() is { Length: > 0 } found ? found : null,
			"メニューの項目");

		var headers = items.Select(i => i.Header as string).ToArray();
		if (!headers.SequenceEqual(expectedItems))
			throw new InvalidOperationException($"メニューの項目が一致しません: {string.Join(" / ", headers)}");
		return items;
	}

	/// <summary>
	/// 確認ダイアログが表示されるのを待って「はい」を押す
	/// </summary>
	public static async Task ConfirmDialogAsync(Window window)
	{
		await ClickByContentAsync(await WaitForControlAsync<FAContentDialog>(window), "はい");
		await WaitUntilAsync(() => !window.GetVisualDescendants().OfType<FAContentDialog>().Any(), "確認ダイアログが閉じる");
	}
}
#endif
