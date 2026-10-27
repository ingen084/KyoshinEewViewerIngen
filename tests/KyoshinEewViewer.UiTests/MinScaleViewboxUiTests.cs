using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using KyoshinEewViewer.CustomControl;

namespace KyoshinEewViewer.UiTests;

/// <summary>
/// 表示領域に合わせて縮小するコントロールが、表示領域が無い場合にも落ちないことを確認する
/// </summary>
public sealed class MinScaleViewboxUiTests
{
	[AvaloniaFact]
	public void 表示領域の幅か高さが0でも_レイアウトで例外にならない()
	{
		var viewbox = new MinScaleViewbox { MinViewWidth = 800, MinViewHeight = 600, Child = new Border() };

		foreach (var size in new[] { new Size(0, 600), new Size(800, 0), new Size(0, 0) })
		{
			viewbox.Measure(size);
			viewbox.Arrange(new Rect(size));
			Assert.True(viewbox.EffectiveScale > 0, $"{size} で縮小率が {viewbox.EffectiveScale} になりました。");
		}
	}
}
