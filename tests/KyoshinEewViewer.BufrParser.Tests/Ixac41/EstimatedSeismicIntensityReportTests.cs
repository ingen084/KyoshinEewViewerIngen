using KyoshinEewViewer.BufrParser.Ixac41;
using KyoshinEewViewer.BufrParser.Tests.TestData;
using Xunit;

namespace KyoshinEewViewer.BufrParser.Tests.Ixac41;

public class EstimatedSeismicIntensityReportTests
{
	[Fact(DisplayName = "合成電文をパースすると電文種類・発生時刻・震源諸元・階級震度テーブル・メッシュがすべて復元される")]
	public void Parse_全プロパティが正しく復元される()
	{
		var originTime = new DateTime(2026, 6, 24, 22, 36, 0, DateTimeKind.Utc);
		var data = new BufrMessageBuilder()
			.AddIntensityClass(1, 0, 4, 3.5f, 4.4f)
			.AddIntensityClass(1, 1, 5, 4.5f, 4.9f)
			.AddIntensityClass(2, 2, 5, 5.0f, 5.4f)
			.SetMessageType(0)
			.SetOriginTimeUtc(originTime)
			.SetEpicenter(areaCode: 286, latitude: 40.6, longitude: 141.3, depthKilometers: 68, magnitudeRaw: 61)
			// 1つ目のL2グループ内に複数のL3・複数の葉を配置し、ネスト反復の復元を確認する
			.AddMesh(52, 40, 3, 2, 5, 6, 1, 1, 35)
			.AddMesh(52, 40, 3, 2, 5, 6, 2, 3, 40)
			.AddMesh(52, 40, 3, 2, 5, 7, 4, 4, 30)
			// 2つ目のL2グループ
			.AddMesh(52, 40, 4, 3, 1, 1, 1, 1, 45)
			.Build();

		var report = EstimatedSeismicIntensityReport.Parse(data);

		Assert.Equal(0, report.MessageType);
		Assert.Equal(originTime, report.OriginTimeUtc);
		Assert.Equal(DateTimeKind.Utc, report.OriginTimeUtc.Kind);
		Assert.Equal(286, report.EpicenterAreaCode);
		Assert.Equal(40.6, report.HypocenterLatitude, precision: 2);
		Assert.Equal(141.3, report.HypocenterLongitude, precision: 2);
		Assert.Equal(68, report.DepthKilometers, precision: 6);
		Assert.Equal(6.1f, report.Magnitude);
		Assert.False(report.IsHugeMagnitude);

		Assert.Equal(3, report.IntensityClasses.Count);
		Assert.Equal(new IntensityClassDefinition(1, 0, 4, 3.5f, 4.4f), report.IntensityClasses[0]);
		Assert.Equal(new IntensityClassDefinition(2, 2, 5, 5.0f, 5.4f), report.IntensityClasses[2]);

		Assert.Equal(4, report.Meshes.Count);
		var (expectedLat, expectedLon) = MeshCodeResolver.ResolveSouthWest(52, 40, 3, 2, 5, 6, 1, 1);
		Assert.Contains(report.Meshes, m =>
			Math.Abs(m.SouthWestLatitude - expectedLat) < 1e-9 &&
			Math.Abs(m.SouthWestLongitude - expectedLon) < 1e-9 &&
			Math.Abs(m.CalculatedIntensity - 3.5f) < 1e-4);
	}

	[Fact(DisplayName = "津波オプションの記述子ブロックがあっても後続の震源諸元・メッシュが正しく読める")]
	public void Parse_津波オプション付きでも後続フィールドが正しく読める()
	{
		var data = new BufrMessageBuilder()
			.AddIntensityClass(1, 0, 4, 3.5f, 4.4f)
			.SetMessageType(0)
			.SetOriginTimeUtc(new DateTime(2026, 6, 24, 22, 36, 0, DateTimeKind.Utc))
			.SetTsunamiOption(qualifier: 1, auxPointNo: 12, bearing: 45, distance: 100)
			.SetEpicenter(areaCode: 286, latitude: 40.6, longitude: 141.3, depthKilometers: 68, magnitudeRaw: 61)
			.AddMesh(52, 40, 3, 2, 5, 6, 1, 1, 35)
			.Build();

		var report = EstimatedSeismicIntensityReport.Parse(data);

		Assert.Equal(286, report.EpicenterAreaCode);
		Assert.Equal(40.6, report.HypocenterLatitude, precision: 2);
		Assert.Equal(141.3, report.HypocenterLongitude, precision: 2);
		Assert.Equal(68, report.DepthKilometers, precision: 6);
		Assert.Equal(6.1f, report.Magnitude);
		Assert.Single(report.Meshes);
	}

	[Theory(DisplayName = "マグニチュード生値の特殊値(127=M8超, 0=不明)はnullに、それ以外は10で除算した値になる")]
	[InlineData(61, 6.1f, false)]
	[InlineData(127, null, true)]
	[InlineData(0, null, false)]
	public void Parse_マグニチュードの特殊値が正しく解釈される(int magnitudeRaw, float? expectedMagnitude, bool expectedIsHuge)
	{
		var data = new BufrMessageBuilder()
			.AddIntensityClass(1, 0, 4, 3.5f, 4.4f)
			.SetMessageType(0)
			.SetOriginTimeUtc(new DateTime(2026, 6, 24, 22, 36, 0, DateTimeKind.Utc))
			.SetEpicenter(areaCode: 286, latitude: 40.6, longitude: 141.3, depthKilometers: 68, magnitudeRaw: magnitudeRaw)
			.AddMesh(52, 40, 3, 2, 5, 6, 1, 1, 35)
			.Build();

		var report = EstimatedSeismicIntensityReport.Parse(data);

		Assert.Equal(expectedMagnitude, report.Magnitude);
		Assert.Equal(expectedIsHuge, report.IsHugeMagnitude);
	}
}
