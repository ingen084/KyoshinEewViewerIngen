using KyoshinEewViewer.BufrParser.Ixac41;
using Xunit;

namespace KyoshinEewViewer.BufrParser.Tests.Ixac41;

public class MeshCodeResolverTests
{
	// 基準座標: 1次(27,39)/2次(3,4)/3次(2,5) → 3次メッシュ南西端 (lat3=18.266666666666666, lon3=139.5625)
	// half/quarter(1〜4)の全16通りについて、期待値は同一の計算式をPythonで独立に計算し埋め込んでいる
	[Theory(DisplayName = "2分の1・4分の1メッシュ番号の全16通りの組み合わせで南西端座標を正しく計算できる")]
	[InlineData(1, 1, 18.266666666666666, 139.5625)]
	[InlineData(1, 2, 18.266666666666666, 139.565625)]
	[InlineData(1, 3, 18.26875, 139.5625)]
	[InlineData(1, 4, 18.26875, 139.565625)]
	[InlineData(2, 1, 18.266666666666666, 139.56875)]
	[InlineData(2, 2, 18.266666666666666, 139.571875)]
	[InlineData(2, 3, 18.26875, 139.56875)]
	[InlineData(2, 4, 18.26875, 139.571875)]
	[InlineData(3, 1, 18.270833333333332, 139.5625)]
	[InlineData(3, 2, 18.270833333333332, 139.565625)]
	[InlineData(3, 3, 18.272916666666667, 139.5625)]
	[InlineData(3, 4, 18.272916666666667, 139.565625)]
	[InlineData(4, 1, 18.270833333333332, 139.56875)]
	[InlineData(4, 2, 18.270833333333332, 139.571875)]
	[InlineData(4, 3, 18.272916666666667, 139.56875)]
	[InlineData(4, 4, 18.272916666666667, 139.571875)]
	public void ResolveSouthWest_half_quarter全16通りで期待座標を返す(int half, int quarter, double expectedLat, double expectedLon)
	{
		var (lat, lon) = MeshCodeResolver.ResolveSouthWest(
			primaryMeshLatitude: 27, primaryMeshLongitude: 39,
			secondaryMeshLatitude: 3, secondaryMeshLongitude: 4,
			tertiaryMeshLatitude: 2, tertiaryMeshLongitude: 5,
			halfMeshNumber: half, quarterMeshNumber: quarter);

		Assert.Equal(expectedLat, lat, precision: 9);
		Assert.Equal(expectedLon, lon, precision: 9);
	}

	[Theory(DisplayName = "代表的なメッシュ番号（実サンプル相当）で3次メッシュ基準点からのオフセットが最終セルの大きさと整合する")]
	[InlineData(52, 40, 0, 0, 0, 0, 1, 1)]
	[InlineData(52, 40, 7, 7, 9, 9, 4, 4)]
	[InlineData(0, 0, 0, 0, 0, 0, 1, 1)]
	public void ResolveSouthWest_代表的な入力で座標が計算できる(int m1lat, int m1lon, int m2lat, int m2lon, int m3lat, int m3lon, int half, int quarter)
	{
		var (lat, lon) = MeshCodeResolver.ResolveSouthWest(m1lat, m1lon, m2lat, m2lon, m3lat, m3lon, half, quarter);

		// 3次メッシュ南西端（half/quarterのオフセットを含まない基準点）
		var tertiaryLat = m1lat * (2.0 / 3.0) + m2lat * (5.0 / 60.0) + m3lat * (0.5 / 60.0);
		var tertiaryLon = m1lon + 100.0 + m2lon * (7.5 / 60.0) + m3lon * (0.75 / 60.0);

		// half/quarterによる最大オフセットは3次メッシュ1マス分（緯度30秒・経度45秒）未満
		Assert.InRange(lat - tertiaryLat, 0, 30.0 / 3600.0);
		Assert.InRange(lon - tertiaryLon, 0, 45.0 / 3600.0);
	}
}
