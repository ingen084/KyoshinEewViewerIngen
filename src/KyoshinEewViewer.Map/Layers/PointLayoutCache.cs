using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Map.Layers;

/// <summary>
/// 点集合のスクリーン座標レイアウト(<see cref="PointLayout{TItem}"/>)を2段階でキャッシュする。
/// 1段目: 元配列の参照単位で、ズーム非依存の正規化座標(投影結果)と空間インデックスを保持する
/// 2段目: (元配列, ズーム)単位で、絶対ピクセル座標を保持する
/// これにより投影計算(三角関数)は点ごとに1回で済み、ズーム変更時はスケーリングのみで再構築できる。
/// Render(コンポジタスレッド)とポインタイベント(UIスレッド)の双方から呼ばれるため、
/// キャッシュは構築完了後の参照差し替えのみで公開する(競合しても構築が重複するだけで結果は等価)
/// </summary>
/// <param name="locationSelector">点の座標を取得するセレクタ。null を返した点はヒットテストの対象外になる</param>
public sealed class PointLayoutCache<TItem>(Func<TItem, Location?> locationSelector)
{
	private NormalizedPointSet<TItem>? _normalized;
	private PointLayout<TItem>? _layout;

	/// <summary>
	/// 指定した配列・ズームに対応するレイアウトを取得する。前回と同じ配列参照・ズームの場合はキャッシュを返す
	/// </summary>
	public PointLayout<TItem> Get(TItem[] source, double zoom)
	{
		var layout = _layout;
		if (layout != null && ReferenceEquals(layout.Source, source) && layout.Zoom == zoom)
			return layout;

		var normalized = _normalized;
		if (normalized == null || !ReferenceEquals(normalized.Source, source))
		{
			normalized = NormalizedPointSet<TItem>.Build(source, locationSelector);
			_normalized = normalized;
		}

		layout = new PointLayout<TItem>(normalized, zoom);
		_layout = layout;
		return layout;
	}
}
