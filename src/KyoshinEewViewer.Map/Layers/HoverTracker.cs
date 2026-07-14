using System;

namespace KyoshinEewViewer.Map.Layers;

/// <summary>
/// レイヤー上でホバー中の点のインデックスを管理する。
/// 元配列が差し替えられて添字の意味が変わる場合は、内容の一致判定でホバー対象を新配列から探し直す
/// </summary>
/// <param name="isSameItem">2つの要素が同じ対象を指すかを判定するデリゲート</param>
public sealed class HoverTracker<TItem>(Func<TItem, TItem, bool> isSameItem)
{
	public int? HoveredIndex { get; private set; }

	/// <summary>
	/// ホバー対象を設定し、変化した場合(=再描画が必要な場合)に true を返す
	/// </summary>
	public bool SetHovered(int? index)
	{
		if (HoveredIndex == index)
			return false;
		HoveredIndex = index;
		return true;
	}

	/// <summary>
	/// 元配列の差し替えに合わせて、ホバー中の点を内容の一致判定で新配列から探し直す。見つからない場合はホバーを解除する
	/// </summary>
	public void Rebind(TItem[]? oldSource, TItem[]? newSource)
	{
		if (HoveredIndex is not { } hoveredIndex)
			return;
		if (oldSource == null || hoveredIndex >= oldSource.Length || newSource is not { Length: > 0 })
		{
			HoveredIndex = null;
			return;
		}

		var hovered = oldSource[hoveredIndex];
		for (var i = 0; i < newSource.Length; i++)
		{
			if (isSameItem(hovered, newSource[i]))
			{
				HoveredIndex = i;
				return;
			}
		}
		HoveredIndex = null;
	}
}
