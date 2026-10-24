using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Desktop.Services;

/// <summary>
/// 配置を記録した時点のモニタ情報。Screen 自体は構成変更時に書き換えられるため保持しない。
/// </summary>
/// <param name="Id">モニタを区別する識別子。取得できない場合は null</param>
internal sealed record WindowPlacementScreen(string? Id, PixelRect Bounds, PixelRect WorkingArea)
{
	/// <summary>
	/// ウィンドウの左上から、タイトルバー付近とみなす点までの距離
	/// </summary>
	private const int TitleBarOffset = 20;

	/// <summary>
	/// ウィンドウのタイトルバー付近が領域内にあるかどうか
	/// </summary>
	public static bool ContainsTitleBar(PixelRect area, PixelPoint position)
		=> area.ContainsExclusive(new PixelPoint(position.X + TitleBarOffset, position.Y + TitleBarOffset));

	/// <summary>
	/// 同じモニタかどうか。識別子が取れないモニタ同士は位置で比較する
	/// </summary>
	public bool IsSameScreen(WindowPlacementScreen other)
		=> Id != null || other.Id != null ? Id == other.Id : Bounds == other.Bounds;

	public WindowPlacementScreen? FindCurrentScreen(IEnumerable<WindowPlacementScreen> screens)
	{
		// 保存先のモニタが消えた場所を別のモニタが占めても、そのモニタには復元しない。
		var matches = screens.Where(IsSameScreen).Take(2).ToArray();
		return matches.Length == 1 ? matches[0] : null;
	}

	public PixelPoint TranslatePosition(PixelPoint position, WindowPlacementScreen current)
	{
		// 解像度が変わってもモニタ内の相対的な位置を保つ。拡大率だけの変更では物理的な位置は変わらない。
		var x = current.Bounds.X + Scale(position.X - Bounds.X, Bounds.Width, current.Bounds.Width);
		var y = current.Bounds.Y + Scale(position.Y - Bounds.Y, Bounds.Height, current.Bounds.Height);
		var translated = new PixelPoint(x, y);
		// 解像度や作業領域が縮んでタイトルバーが隠れる場合は、画面内に残す。元から作業領域の外に置かれていた場合は動かさない。
		var area = current.WorkingArea;
		if (area.Width <= 0 || area.Height <= 0 || !ContainsTitleBar(WorkingArea, position) || ContainsTitleBar(area, translated))
			return translated;
		return new(
			Math.Clamp(x, area.X, Math.Max(area.X, area.Right - TitleBarOffset * 2)),
			Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - TitleBarOffset * 2)));
	}

	private static int Scale(int offset, int from, int to)
		=> from > 0 && to > 0 ? (int)Math.Round(offset * (double)to / from) : offset;
}
