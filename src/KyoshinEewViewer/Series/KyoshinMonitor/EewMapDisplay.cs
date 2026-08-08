using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

/// <summary>
/// 複数の緊急地震速報を地図上で識別するための表示情報を組み立てる。
/// </summary>
internal static class EewMapDisplay
{
	/// <summary>継続中のEEWと同時表示する最終報の描画透明度</summary>
	internal const byte FinalContentOpacity = 96;
	/// <summary>最終報・キャンセル報のラベル透明度。状態を判別できるよう本文より濃くする</summary>
	internal const byte InactiveLabelOpacity = 170;

	private const float MarkerGap = 3;
	private const float OffsetLabelGap = 12;
	private const float CandidateGap = 4;

	internal readonly record struct LabelItem(int SourceIndex, Eew Eew, SKPoint Center, float MarkerRadius);
	internal readonly record struct LabelPlacement(
		int SourceIndex,
		string[] Lines,
		SKRect Rect,
		(SKPoint Start, SKPoint End)? LeaderLine,
		byte Opacity);

	internal static bool ShouldShowLabels(IReadOnlyCollection<Eew> eews)
		=> eews.Count > 1;

	/// <summary>
	/// 最終報を減光すべき組み合わせかを判定する。
	/// キャンセル報は継続中・最終報のどちらにも数えず、従来どおり個別に減光する。
	/// </summary>
	internal static bool ShouldDimFinalEews(IReadOnlyCollection<Eew> eews)
		=> eews.Count > 1
		&& eews.Any(e => !e.IsCancelled && !e.IsFinal)
		&& eews.Any(e => !e.IsCancelled && e.IsFinal);

	internal static byte GetContentOpacity(Eew eew, bool dimFinalEews)
		=> !eew.IsCancelled && eew.IsFinal && dimFinalEews
			? FinalContentOpacity
			: byte.MaxValue;

	internal static byte GetLabelOpacity(Eew eew, bool dimFinalEews)
		=> eew.IsCancelled || (eew.IsFinal && dimFinalEews)
			? InactiveLabelOpacity
			: byte.MaxValue;

	internal static SKColor ApplyOpacity(SKColor color, byte opacity)
		=> color.WithAlpha((byte)((color.Alpha * opacity + 127) / byte.MaxValue));

	/// <summary>
	/// 震源ラベルを「#報数(状態) 震央地名 Mx.x 最大震度x」の形式で組み立てる。
	/// </summary>
	internal static string BuildLabel(Eew eew)
	{
		var status = eew.IsCancelled
			? eew.IsTrueCancelled ? "(取消)" : "(取消?)"
			: eew.IsFinal ? "(終)" : "";
		var place = string.IsNullOrWhiteSpace(eew.Hypocenter?.Place) ? "震源地不明" : eew.Hypocenter.Place;
		var magnitude = eew.Hypocenter?.Magnitude is { } value
			? value.ToString("0.0", CultureInfo.InvariantCulture)
			: "不明";
		var intensity = eew.MaxIntensity == KyoshinMonitorLib.JmaIntensity.Unknown
			? "不明"
			: KyoshinMonitorLib.JmaIntensityExtensions.ToLongString(eew.MaxIntensity).Replace("震度", "");
		if (eew.IsIntensityOver && eew.MaxIntensity != KyoshinMonitorLib.JmaIntensity.Unknown)
			intensity += "以上";

		return $"#{eew.SerialNo}{status} {place} M{magnitude} 最大震度{intensity}";
	}

	/// <summary>
	/// 継続中→最終報→キャンセル報の順でラベルを配置する。
	/// 他のEEWの震源記号と配置済みラベルに重ならず、表示領域に収まる候補だけを採用する。
	/// </summary>
	internal static LabelPlacement?[] BuildLabelLayout(
		IReadOnlyList<LabelItem> items,
		SKRect viewport,
		bool dimFinalEews,
		Func<string[], (float Width, float Height)>? measureLines = null)
	{
		var placements = new LabelPlacement?[items.Count];
		if (items.Count == 0 || viewport.IsEmpty)
			return placements;
		measureLines ??= MapLayerLabelRenderer.MeasureLines;

		var occupiedRects = new List<SKRect>(items.Count * 2);
		foreach (var item in items)
		{
			var markerRadius = item.MarkerRadius + 2;
			occupiedRects.Add(SKRect.Create(
				item.Center.X - markerRadius,
				item.Center.Y - markerRadius,
				markerRadius * 2,
				markerRadius * 2));
		}

		foreach (var indexedItem in items
			.Select((item, index) => (Item: item, PlacementIndex: index))
			.OrderBy(i => GetPriority(i.Item.Eew))
			.ThenByDescending(i => i.Item.Eew.Hypocenter?.OccurrenceTime)
			.ThenBy(i => i.Item.SourceIndex))
		{
			var item = indexedItem.Item;
			string[] lines = [BuildLabel(item.Eew)];
			var (width, height) = measureLines(lines);
			foreach (var (rect, isDefault) in BuildCandidates(item.Center, width, height, item.MarkerRadius, items.Count))
			{
				if (!Contains(viewport, rect) || occupiedRects.Exists(r => r.IntersectsWith(rect)))
					continue;

				placements[indexedItem.PlacementIndex] = new LabelPlacement(
					item.SourceIndex,
					lines,
					rect,
					isDefault ? null : MapLayerLabelRenderer.ComputeLeaderLine(item.Center, rect, item.MarkerRadius),
					GetLabelOpacity(item.Eew, dimFinalEews));
				occupiedRects.Add(rect);
				break;
			}
		}

		return placements;
	}

	private static int GetPriority(Eew eew)
		=> eew.IsCancelled ? 2 : eew.IsFinal ? 1 : 0;

	private static bool Contains(SKRect outer, SKRect inner)
		=> inner.Left >= outer.Left && inner.Top >= outer.Top
		&& inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

	/// <summary>
	/// 右・左・上・下を最初に試し、その周囲へ上下または左右にずらした候補を広げる。
	/// 同一震源付近に多数のEEWがある場合も、単純な4方向だけでラベルが欠落しないようにする。
	/// </summary>
	private static IEnumerable<(SKRect Rect, bool IsDefault)> BuildCandidates(
		SKPoint center,
		float width,
		float height,
		float markerRadius,
		int itemCount)
	{
		var near = MapLayerLabelRenderer.BuildDirectionalCandidates(
			center,
			width,
			height,
			markerRadius + MarkerGap,
			markerRadius + OffsetLabelGap);
		for (var i = 0; i < near.Length; i++)
			yield return (near[i], i == 0);

		var verticalStep = height + CandidateGap;
		var horizontalStep = width + CandidateGap;
		for (var step = 1; step <= itemCount; step++)
		{
			var dy = step * verticalStep;
			var dx = step * horizontalStep;

			// 右・左の候補を上下へ、上・下の候補を左右へ交互に広げる。
			yield return (Offset(near[0], 0, -dy), false);
			yield return (Offset(near[0], 0, dy), false);
			yield return (Offset(near[1], 0, -dy), false);
			yield return (Offset(near[1], 0, dy), false);
			yield return (Offset(near[2], -dx, 0), false);
			yield return (Offset(near[2], dx, 0), false);
			yield return (Offset(near[3], -dx, 0), false);
			yield return (Offset(near[3], dx, 0), false);
		}
	}

	private static SKRect Offset(SKRect rect, float dx, float dy)
		=> SKRect.Create(rect.Left + dx, rect.Top + dy, rect.Width, rect.Height);
}
