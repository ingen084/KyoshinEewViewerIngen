using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Map;

/// <summary>
/// PointLayoutCache / PointLayout のユニットテスト。
/// 点数が少ない場合(線形探索)と多い場合(グリッド索引)とで結果が一致することの検証が核心となる。
/// </summary>
public class PointLayoutCacheTests
{
	private sealed record TestPoint(Location? Location);

	private static readonly Location Sapporo = new(43.0621f, 141.3544f);
	private static readonly Location Tokyo = new(35.6812f, 139.7671f);
	private static readonly Location Osaka = new(34.6937f, 135.5023f);
	private static readonly Location Naha = new(26.2124f, 127.6809f);

	[Fact(DisplayName = "ピクセル座標がToPixelと一致する")]
	public void ピクセル座標がToPixelと一致する()
	{
		var points = new TestPoint[]
		{
			new(Sapporo),
			new(Tokyo),
			new(Osaka),
			new(Naha),
			new(null), // 座標が取得できない点
		};

		var cache = new PointLayoutCache<TestPoint>(p => p.Location);

		foreach (var zoom in new[] { 4d, 7.5d, 10d })
		{
			var layout = cache.Get(points, zoom);
			Assert.Equal(zoom, layout.Zoom);

			for (var i = 0; i < points.Length; i++)
			{
				var expected = points[i].Location is { } location
					? location.ToPixel(zoom)
					: new PointD(double.NaN, double.NaN);
				// PointD は double のフィールド比較となるため、NaN同士も Equals では一致扱いになる
				Assert.Equal(expected, layout.Pixels[i]);
			}
		}
	}

	[Fact(DisplayName = "FindNearestの基本動作")]
	public void FindNearestの基本動作()
	{
		var points = new TestPoint[]
		{
			new(Tokyo),
			new(null), // 座標が取得できない点
			new(Osaka),
		};

		const double zoom = 6d;
		var cache = new PointLayoutCache<TestPoint>(p => p.Location);
		var layout = cache.Get(points, zoom);

		var tokyoPixel = Tokyo.ToPixel(zoom);
		var osakaPixel = Osaka.ToPixel(zoom);

		// (a) 閾値未満の距離にある点を指すとその点が返る
		var near = tokyoPixel + new PointD(3, 4); // 距離5
		Assert.Equal(0, layout.FindNearest(near, thresholdPixel: 10));

		// (b) どの点からも閾値以上離れた位置ではnull
		var far = osakaPixel + new PointD(1000, 1000);
		Assert.Null(layout.FindNearest(far, thresholdPixel: 10));

		// (c) 閾値ちょうどの距離では「未満」でないためnull
		var exactlyAtThreshold = osakaPixel + new PointD(10, 0); // 距離ちょうど10
		Assert.Null(layout.FindNearest(exactlyAtThreshold, thresholdPixel: 10));

		// (d) 座標が取得できない点(index=1)は、閾値をどれだけ広げても絶対にヒットしない
		Assert.Equal(0, layout.FindNearest(tokyoPixel, thresholdPixel: 1e9));
	}

	[Fact(DisplayName = "グリッド経路と線形全走査の結果が一致する")]
	public void グリッド経路と線形全走査の結果が一致する()
	{
		var random = new Random(42);
		var points = new TestPoint[500];
		for (var i = 0; i < points.Length; i++)
		{
			// 約1割の点は座標を持たない(nullを混ぜる)
			if (random.NextDouble() < 0.1)
			{
				points[i] = new TestPoint(null);
				continue;
			}
			var lat = (float)(24 + random.NextDouble() * (46 - 24));
			var lng = (float)(123 + random.NextDouble() * (146 - 123));
			points[i] = new TestPoint(new Location(lat, lng));
		}

		const double zoom = 8d;
		var cache = new PointLayoutCache<TestPoint>(p => p.Location);
		var layout = cache.Get(points, zoom);
		const double threshold = 10d;

		var validIndices = new List<int>();
		for (var i = 0; i < layout.Pixels.Length; i++)
		{
			if (!double.IsNaN(layout.Pixels[i].X))
				validIndices.Add(i);
		}

		var actualResults = new List<(int Index, double Distance)>();
		for (var q = 0; q < 100; q++)
		{
			// 実在する点の近傍を中心に、±20px程度ばらしたクエリ位置を作る
			var baseIndex = validIndices[random.Next(validIndices.Count)];
			var query = layout.Pixels[baseIndex] + new PointD(random.NextDouble() * 40 - 20, random.NextDouble() * 40 - 20);

			var (expectedNearest, expectedSet) = BruteForceSearch(layout.Pixels, query, threshold);

			var actualNearest = layout.FindNearest(query, threshold);
			Assert.Equal(expectedNearest, actualNearest);

			actualResults.Clear();
			layout.CollectWithin(query, threshold, actualResults);
			var actualSet = new HashSet<int>(actualResults.Select(r => r.Index));
			Assert.True(expectedSet.SetEquals(actualSet), $"クエリ{q}: CollectWithinの結果集合が一致しない");

			foreach (var (index, distance) in actualResults)
			{
				var realDistance = (layout.Pixels[index] - query).Length;
				Assert.True(Math.Abs(realDistance - distance) < 1e-6, $"クエリ{q}, index={index}: 距離の誤差が大きい");
			}
		}
	}

	/// <summary>Pixelsを全走査して、閾値未満の最近傍インデックスと該当インデックス集合を求める(期待値算出用)</summary>
	private static (int? Nearest, HashSet<int> Within) BruteForceSearch(PointD[] pixels, PointD query, double thresholdPixel)
	{
		int? nearest = null;
		var thresholdSq = thresholdPixel * thresholdPixel;
		var nearestSq = thresholdSq;
		var within = new HashSet<int>();

		for (var i = 0; i < pixels.Length; i++)
		{
			var p = pixels[i];
			if (double.IsNaN(p.X))
				continue;
			var dx = p.X - query.X;
			var dy = p.Y - query.Y;
			var distanceSq = dx * dx + dy * dy;
			if (distanceSq < thresholdSq)
				within.Add(i);
			if (distanceSq < nearestSq)
			{
				nearestSq = distanceSq;
				nearest = i;
			}
		}
		return (nearest, within);
	}

	[Fact(DisplayName = "キャッシュの再利用と無効化")]
	public void キャッシュの再利用と無効化()
	{
		var points = new TestPoint[] { new(Tokyo), new(Osaka) };
		var cache = new PointLayoutCache<TestPoint>(p => p.Location);

		// 同一配列・同一ズームでは同一インスタンスを返す
		var layout1 = cache.Get(points, 5);
		var layout1Again = cache.Get(points, 5);
		Assert.Same(layout1, layout1Again);

		// ズーム変更で別インスタンスになるが、座標は新ズームのToPixelと一致する
		var layout2 = cache.Get(points, 6);
		Assert.NotSame(layout1, layout2);
		for (var i = 0; i < points.Length; i++)
			Assert.Equal(points[i].Location!.ToPixel(6), layout2.Pixels[i]);

		// 内容が同一でも配列インスタンスが異なれば別インスタンスとして扱われる
		var pointsCopy = (TestPoint[])points.Clone();
		var layout3 = cache.Get(pointsCopy, 6);
		Assert.NotSame(layout2, layout3);
		for (var i = 0; i < pointsCopy.Length; i++)
			Assert.Equal(pointsCopy[i].Location!.ToPixel(6), layout3.Pixels[i]);
	}
}
