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
	private const double EarthRadius = 6371.0; // km

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
	/// 凸包にオフセットを適用
	/// </summary>
	/// <param name="hull">凸包の頂点（反時計回り）</param>
	/// <param name="offsetKm">オフセット距離（km）</param>
	/// <returns>オフセット適用後の頂点リスト</returns>
	public static List<Location> ApplyOffset(List<Location> hull, double offsetKm)
	{
		if (hull.Count < 3)
			return hull;

		var result = new List<Location>(hull.Count);

		for (var i = 0; i < hull.Count; i++)
		{
			var prev = hull[(i - 1 + hull.Count) % hull.Count];
			var curr = hull[i];
			var next = hull[(i + 1) % hull.Count];

			var offsetPoint = GetOffsetVertex(prev, curr, next, offsetKm);
			result.Add(offsetPoint);
		}

		return result;
	}

	/// <summary>
	/// カプセル形状の頂点リストを生成（2点用）
	/// </summary>
	/// <param name="p1">点1</param>
	/// <param name="p2">点2</param>
	/// <param name="offsetKm">オフセット距離（km）</param>
	/// <param name="arcDivisions">円弧の分割数</param>
	/// <returns>カプセル形状の頂点リスト</returns>
	public static List<Location> CreateCapsuleVertices(Location p1, Location p2, double offsetKm, int arcDivisions = 16)
	{
		var result = new List<Location>();

		// 2点間の方向ベクトル（度単位）
		var dx = p2.Longitude - p1.Longitude;
		var dy = p2.Latitude - p1.Latitude;

		// p1の緯度での経度補正
		var avgLat = (p1.Latitude + p2.Latitude) / 2.0;
		var lonScale = Math.Cos(avgLat * Math.PI / 180.0);

		// 正規化された方向ベクトル（緯度経度空間で）
		var length = Math.Sqrt(dx * dx * lonScale * lonScale + dy * dy);
		if (length < 1e-10)
		{
			// 2点が同一の場合は円として扱う
			return CreateCircleVertices(p1, offsetKm);
		}

		var ndx = dx / length * lonScale;
		var ndy = dy / length;

		// 垂直方向（右手系で90度回転）
		var perpX = -ndy / lonScale;
		var perpY = ndx;

		// オフセット距離を緯度経度に変換
		var latOffset = offsetKm / 111.0;
		var lonOffset = offsetKm / (111.0 * lonScale);

		// p1側の半円（p2からp1方向に向かって左側から開始）
		var startAngle1 = Math.Atan2(-ndy, -ndx * lonScale);
		for (var i = 0; i <= arcDivisions / 2; i++)
		{
			var angle = startAngle1 - Math.PI / 2 + Math.PI * i / (arcDivisions / 2);
			var lat = p1.Latitude + latOffset * Math.Sin(angle);
			var lon = p1.Longitude + lonOffset * Math.Cos(angle);
			result.Add(new Location((float)lat, (float)lon));
		}

		// p2側の半円
		var startAngle2 = Math.Atan2(ndy, ndx * lonScale);
		for (var i = 0; i <= arcDivisions / 2; i++)
		{
			var angle = startAngle2 - Math.PI / 2 + Math.PI * i / (arcDivisions / 2);
			var lat = p2.Latitude + latOffset * Math.Sin(angle);
			var lon = p2.Longitude + lonOffset * Math.Cos(angle);
			result.Add(new Location((float)lat, (float)lon));
		}

		return result;
	}

	/// <summary>
	/// 円形状の頂点リストを生成（1点用）
	/// </summary>
	/// <param name="center">中心点</param>
	/// <param name="radiusKm">半径（km）</param>
	/// <param name="divisions">分割数</param>
	/// <returns>円形状の頂点リスト</returns>
	public static List<Location> CreateCircleVertices(Location center, double radiusKm, int divisions = 32)
	{
		var result = new List<Location>(divisions);

		var latOffset = radiusKm / 111.0;
		var lonScale = Math.Cos(center.Latitude * Math.PI / 180.0);
		var lonOffset = radiusKm / (111.0 * lonScale);

		for (var i = 0; i < divisions; i++)
		{
			var angle = 2 * Math.PI * i / divisions;
			var lat = center.Latitude + latOffset * Math.Sin(angle);
			var lon = center.Longitude + lonOffset * Math.Cos(angle);
			result.Add(new Location((float)lat, (float)lon));
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

	/// <summary>
	/// 頂点のオフセット位置を計算
	/// </summary>
	private static Location GetOffsetVertex(Location prev, Location curr, Location next, double offsetKm)
	{
		// 緯度による経度補正
		var lonScale = Math.Cos(curr.Latitude * Math.PI / 180.0);

		// 辺1: prev -> curr の方向ベクトル
		double e1x = (curr.Longitude - prev.Longitude) * lonScale;
		double e1y = curr.Latitude - prev.Latitude;
		var len1 = Math.Sqrt(e1x * e1x + e1y * e1y);
		if (len1 > 0) { e1x /= len1; e1y /= len1; }

		// 辺2: curr -> next の方向ベクトル
		double e2x = (next.Longitude - curr.Longitude) * lonScale;
		double e2y = next.Latitude - curr.Latitude;
		var len2 = Math.Sqrt(e2x * e2x + e2y * e2y);
		if (len2 > 0) { e2x /= len2; e2y /= len2; }

		// 各辺の外向き法線（反時計回りの凸包では右手側が外）
		// 辺の方向ベクトル(x,y)に対して、外向き法線は(y,-x)
		double n1x = e1y;
		double n1y = -e1x;
		double n2x = e2y;
		double n2y = -e2x;

		// 2つの法線の平均（角の二等分線方向）
		double nx = n1x + n2x;
		double ny = n1y + n2y;
		var nLen = Math.Sqrt(nx * nx + ny * ny);
		if (nLen > 0) { nx /= nLen; ny /= nLen; }

		// オフセット距離を緯度経度に変換（鋭角補正なし、角丸で対応）
		var latOffset = offsetKm / 111.0;
		var lonOffset = offsetKm / (111.0 * lonScale);

		return new Location(
			(float)(curr.Latitude + ny * latOffset),
			(float)(curr.Longitude + nx * lonOffset / lonScale)
		);
	}
}
