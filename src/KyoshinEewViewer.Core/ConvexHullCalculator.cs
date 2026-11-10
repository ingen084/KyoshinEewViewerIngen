using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core;

/// <summary>
/// 凸包計算とオフセット適用のユーティリティクラス
/// </summary>
public static class ConvexHullCalculator
{
	private const double KmPerDegree = 111.0;

	/// <summary>
	/// 円弧を折れ線で近似するときの1区間の角度（ラジアン）
	/// </summary>
	private const double ArcStepRadians = 2 * Math.PI / 64;

	/// <summary>
	/// Monotone Chain アルゴリズムで凸包を計算
	/// </summary>
	/// <param name="points">入力点群</param>
	/// <returns>凸包を構成する点のリスト（反時計回り）</returns>
	public static List<Location> ComputeConvexHull(IEnumerable<Location> points)
	{
		var sorted = points
			.OrderBy(p => p.Longitude)
			.ThenBy(p => p.Latitude)
			.ToList();

		if (sorted.Count < 3)
			return sorted;

		var lower = new List<Location>();
		foreach (var p in sorted)
		{
			while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
				lower.RemoveAt(lower.Count - 1);
			lower.Add(p);
		}

		var upper = new List<Location>();
		for (var i = sorted.Count - 1; i >= 0; i--)
		{
			var p = sorted[i];
			while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
				upper.RemoveAt(upper.Count - 1);
			upper.Add(p);
		}

		// 最後の点は次のリストの最初と重複するため除去
		lower.RemoveAt(lower.Count - 1);
		upper.RemoveAt(upper.Count - 1);

		lower.AddRange(upper);
		return lower;
	}

	/// <summary>
	/// 凸包から一定距離の範囲を囲む図形の頂点リストを生成する。
	/// 各辺を外側へ平行移動し、頂点のまわりを円弧でつなぐため、
	/// 図形は凸包のどの点からも <paramref name="offsetKm"/> 以上離れる
	/// </summary>
	/// <param name="hull">凸包の頂点（反時計回り）。1点なら円、2点ならカプセル形状になる</param>
	/// <param name="offsetKm">凸包からの距離（km）</param>
	/// <returns>図形の頂点リスト（反時計回り）</returns>
	public static List<Location> CreateRoundedOffsetPolygon(IReadOnlyList<Location> hull, double offsetKm)
	{
		var vertices = RemoveDuplicateVertices(hull);
		var result = new List<Location>();
		if (vertices.Count == 0)
			return result;

		if (vertices.Count == 1)
		{
			AddArc(result, vertices[0], 0, 2 * Math.PI, offsetKm);
			return result;
		}

		for (var i = 0; i < vertices.Count; i++)
		{
			var prev = vertices[(i - 1 + vertices.Count) % vertices.Count];
			var curr = vertices[i];
			var next = vertices[(i + 1) % vertices.Count];

			var lonScale = Math.Cos(curr.Latitude * Math.PI / 180.0);
			var startAngle = GetOutwardNormalAngle(prev, curr, lonScale);
			var sweep = GetOutwardNormalAngle(curr, next, lonScale) - startAngle;
			if (sweep < 0)
				sweep += 2 * Math.PI;
			// 凸包の外角は π 以下なので、2π 近くになるのは誤差でわずかに負になった 0
			if (sweep > Math.PI * 1.5)
				sweep = 0;

			AddArc(result, curr, startAngle, sweep, offsetKm);
		}

		return result;
	}

	/// <summary>
	/// 外積の計算（2次元）
	/// </summary>
	private static double Cross(Location o, Location a, Location b)
	{
		return (a.Longitude - o.Longitude) * (b.Latitude - o.Latitude)
			 - (a.Latitude - o.Latitude) * (b.Longitude - o.Longitude);
	}

	private static List<Location> RemoveDuplicateVertices(IReadOnlyList<Location> vertices)
	{
		var result = new List<Location>(vertices.Count);
		foreach (var v in vertices)
		{
			if (result.Count == 0 || !IsSameLocation(result[^1], v))
				result.Add(v);
		}
		while (result.Count > 1 && IsSameLocation(result[0], result[^1]))
			result.RemoveAt(result.Count - 1);
		return result;
	}

	private static bool IsSameLocation(Location a, Location b)
		=> a.Latitude == b.Latitude && a.Longitude == b.Longitude;

	/// <summary>
	/// 辺の外向き法線の向き（東を 0 とする反時計回りのラジアン）を求める。
	/// 反時計回りの凸包では辺の進行方向に対して右手側が外側
	/// </summary>
	private static double GetOutwardNormalAngle(Location from, Location to, double lonScale)
	{
		var dx = (to.Longitude - from.Longitude) * lonScale;
		var dy = to.Latitude - from.Latitude;
		return Math.Atan2(-dx, dy);
	}

	private static void AddArc(List<Location> result, Location center, double startAngle, double sweep, double radiusKm)
	{
		var latOffset = radiusKm / KmPerDegree;
		var lonOffset = radiusKm / (KmPerDegree * Math.Cos(center.Latitude * Math.PI / 180.0));
		var divisions = Math.Max(1, (int)Math.Ceiling(sweep / ArcStepRadians));

		for (var i = 0; i <= divisions; i++)
		{
			var angle = startAngle + sweep * i / divisions;
			result.Add(new Location(
				(float)(center.Latitude + latOffset * Math.Sin(angle)),
				(float)(center.Longitude + lonOffset * Math.Cos(angle))));
		}
	}
}
