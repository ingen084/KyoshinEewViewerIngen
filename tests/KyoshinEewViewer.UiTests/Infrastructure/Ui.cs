using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests.Infrastructure;

internal static class Ui
{
	/// <summary>
	/// 実時間のタイマーやバックグラウンド処理を待つため、UIスレッドのジョブとアニメーションを進めながら条件を満たすまで待つ
	/// </summary>
	public static Task WaitUntilAsync(Func<bool> condition, string description, double timeoutSeconds = 5)
		=> WaitUntilAsync(condition, () => description, timeoutSeconds);

	/// <param name="describe">失敗時の説明。待機後の状態を含められるよう、失敗したときに生成する</param>
	public static async Task WaitUntilAsync(Func<bool> condition, Func<string> describe, double timeoutSeconds = 5)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		while (true)
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			if (condition())
				return;
			if (DateTime.UtcNow >= deadline)
				throw new XunitException($"{timeoutSeconds}秒以内に条件を満たしませんでした: {describe()}");
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}

	/// <summary>
	/// 表示されているテキストを列挙する
	/// </summary>
	public static string[] VisibleTexts(Visual root)
		=> [.. root.GetVisualDescendants().OfType<TextBlock>()
			.Where(t => t.IsEffectivelyVisible)
			.Select(t => string.IsNullOrEmpty(t.Text) ? t.Inlines?.Text : t.Text)
			.OfType<string>()];

	public static Button FindButton(Visual root, string content)
		=> root.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, content));

	/// <summary>
	/// コントロールの中央をマウスでクリックする
	/// </summary>
	public static void Click(Window window, Control control)
	{
		if (control is not Button button)
		{
			Press(window, control);
			return;
		}

		// ヘッドレス環境では、直前に表示が変わったボタンを押しても離したときのヒットテストに失敗して反応しないことがあるため、
		// ボタンが押されたことを確認できるまで押し直す
		var clicked = false;
		void OnClick(object? sender, RoutedEventArgs e) => clicked = true;
		button.Click += OnClick;
		try
		{
			for (var i = 0; i < 5 && !clicked; i++)
			{
				Press(window, button);
				Dispatcher.UIThread.RunJobs();
			}
		}
		finally
		{
			button.Click -= OnClick;
		}
		if (!clicked)
			throw new InvalidOperationException($"{button.Content} ボタンを押せません。");
	}

	private static void Press(Window window, Control control)
	{
		// スクロールの外にある場合も操作できるよう、表示範囲に入れてから位置を計算する
		control.BringIntoView();
		for (var i = 0; ; i++)
		{
			window.UpdateLayout();
			Dispatcher.UIThread.RunJobs();
			var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
				?? throw new InvalidOperationException("クリック位置を計算できません。");
			// ヒットテストは描画結果を使うため、スクロールなどが反映されて対象に当たるようになってから押す
			if (window.InputHitTest(point) is Visual hit && (hit == control || control.IsVisualAncestorOf(hit)))
			{
				window.MouseDown(point, MouseButton.Left);
				window.MouseUp(point, MouseButton.Left);
				return;
			}
			if (i >= 10)
				throw new InvalidOperationException($"{control} をクリックできません。");
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		}
	}

	public static void PressKey(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		window.KeyPressQwerty(key, modifiers);
		window.KeyReleaseQwerty(key, modifiers);
	}
}
