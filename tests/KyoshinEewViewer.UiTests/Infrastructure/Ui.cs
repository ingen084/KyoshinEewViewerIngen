using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests.Infrastructure;

internal static class Ui
{
	/// <summary>
	/// 実時間のタイマーやバックグラウンド処理を待つため、UIスレッドのジョブとアニメーションを進めながら条件を満たすまで待つ
	/// </summary>
	public static async Task WaitUntilAsync(Func<bool> condition, string description, double timeoutSeconds = 5)
	{
		var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
		while (true)
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			if (condition())
				return;
			if (DateTime.UtcNow >= deadline)
				throw new XunitException($"{timeoutSeconds}秒以内に条件を満たしませんでした: {description}");
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
		var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
			?? throw new InvalidOperationException("クリック位置を計算できません。");
		window.MouseDown(point, MouseButton.Left);
		window.MouseUp(point, MouseButton.Left);
	}

	public static void PressKey(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		window.KeyPressQwerty(key, modifiers);
		window.KeyReleaseQwerty(key, modifiers);
	}
}
