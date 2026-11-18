using System;
using System.IO;
using System.Linq;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

// 気象庁のサンプルデータ（2026-09-17 版）を Fixtures に収録（末尾空行のみ整理）。
// https://xml.kishou.go.jp/tec_material.html
// https://xml.kishou.go.jp/jmaxml_20260917_Samples.zip
public class OfficialSampleTests
{
	private static JmaXmlDocument OpenSample(string name)
		=> new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));

	[Fact(DisplayName = "公式の緊急地震速報から未到達区域の予測時刻と到達済み区域の状態を区別できる")]
	public void EewArrivalAndCondition()
	{
		using var doc = OpenSample("36_01_10_240613_VXSE44.xml");
		var forecast = doc.EarthquakeBody.Intensity!.Value.Forecast!.Value;
		var areas = forecast.Prefs.SelectMany(p => p.Areas).ToArray();
		var arrived = areas.First(a => a.Code == 622);
		Assert.Null(arrived.ArrivalTime);
		Assert.Equal("既に主要動到達と推測", arrived.Condition);
		Assert.Equal("5+", arrived.ForecastInt!.Value.From);
		Assert.Equal(arrived.ForecastIntFrom, arrived.ForecastInt!.Value.From);
		Assert.Equal(arrived.ForecastIntTo, arrived.ForecastInt!.Value.To);
		Assert.Null(arrived.ForecastLgInt);

		var expected = areas.First(a => a.Code == 741);
		Assert.Equal(new DateTimeOffset(2024, 4, 17, 23, 15, 25, TimeSpan.FromHours(9)), expected.ArrivalTime);
		Assert.Null(expected.Condition);
		Assert.Equal("00", expected.Category!.Value.Kind.Code);
		Assert.Equal("固定付加文", doc.EarthquakeBody.Comments!.Value.WarningCommentCodeType);
	}

	[Fact(DisplayName = "公式の長周期地震動情報から観測点階級と周期別スペクトルおよび関連URIを取得できる")]
	public void LongPeriodObservation()
	{
		using var doc = OpenSample("78_01_01_240613_VXSE62.xml");
		var observation = doc.EarthquakeBody.Intensity!.Value.Observation!.Value;
		var area = observation.Prefs.SelectMany(p => p.Areas).First(a => a.IntensityStations.Any(s => s.Code == "0421200"));
		var station = area.IntensityStations.First(s => s.Code == "0421200");
		Assert.Equal("登米市中田町", station.Name);
		Assert.Equal("4", station.Int);
		Assert.Equal("3", station.LgInt);
		Assert.Null(station.K);
		Assert.Equal(new[] { "2", "3", "2", "1", "0", "0", "0" }, station.LgIntPerPeriods.Select(p => p.Value));
		Assert.Equal(50.3f, station.SvaPerPeriods.Single(p => p.PeriodicBand == 2).Value);
		Assert.Null(area.Category);
		Assert.Null(area.ForecastInt);
		Assert.Equal("https://www.data.jma.go.jp/eew/data/ltpgm/202011211100000/index.html", doc.EarthquakeBody.Comments!.Value.Uri);
	}

	[Fact(DisplayName = "公式の沖合津波情報から観測機器と沿岸推定値を取得し不明な高さを保持できる")]
	public void OffshoreTsunamiEstimation()
	{
		using var doc = OpenSample("61_11_02_250206_VTSE52.xml");
		var tsunami = doc.TsunamiBody.Tsunami!.Value;
		var stations = tsunami.Observation!.Value.Items.SelectMany(i => i.Stations).ToArray();
		Assert.Equal("水圧計", stations.Single(s => s.Code == 21050).Sensor);
		Assert.Equal("ＧＰＳ波浪計", stations.Single(s => s.Code == 21090).Sensor);
		Assert.Null(stations[0].CurrentHeight);

		var estimation = tsunami.Estimation!.Value;
		Assert.Equal("沿岸地域", Assert.Single(estimation.CodeDefineTypes).Value);
		var items = estimation.Items.ToArray();
		Assert.Equal(2, items.Length);
		Assert.Equal(201, items[0].Area.Code);
		Assert.Equal(new DateTimeOffset(2011, 3, 11, 14, 56, 0, TimeSpan.FromHours(9)), items[0].FirstHeight!.Value.ArrivalTime);
		Assert.Equal("早いところでは既に津波到達と推定", items[0].FirstHeight!.Value.Condition);
		Assert.Equal("追加", items[0].FirstHeight!.Value.Revise);
		var height = items[0].MaxHeight!.Value.TsunamiHeight!.Value;
		Assert.Equal("不明", height.Condition);
		Assert.Equal("巨大", height.Description);
		Assert.Equal("NaN", height.Value);

		var hypocenter = Assert.Single(doc.TsunamiBody.Earthquakes).Hypocenter.Area;
		Assert.Equal("202", hypocenter.MarkCode);
		Assert.Equal("震央補助", hypocenter.MarkCodeType);
		Assert.Equal("東南東", hypocenter.Direction);
		Assert.Equal(130, hypocenter.Distance);
		Assert.Equal("km", hypocenter.DistanceUnit);
	}

	[Theory(DisplayName = "公式の地震関連情報から種別と本文を取得できる")]
	[InlineData("74_01_03_200512_VYSE50.xml", "南海トラフ地震に関連する情報", "南海トラフ地震臨時情報", "113")]
	[InlineData("80_01_01_260917_VYSE60.xml", "北海道・三陸沖後発地震注意情報", "北海道・三陸沖後発地震注意情報", null)]
	public void RelatedEarthquakeInformation(string name, string type, string infoKind, string? code)
	{
		using var doc = OpenSample(name);
		var info = doc.EarthquakeBody.EarthquakeInfo!.Value;
		Assert.Equal(type, info.Type);
		Assert.Equal(infoKind, info.InfoKind);
		Assert.False(string.IsNullOrWhiteSpace(info.Text));
		Assert.Equal(code, info.InfoSerial?.Code);
		if (code != null)
		{
			Assert.Equal("調査中", info.InfoSerial!.Value.Name);
			Assert.Equal("地震関連情報番号コード", info.InfoSerial!.Value.CodeType);
		}
		else
			Assert.False(string.IsNullOrWhiteSpace(info.Appendix));
	}

	[Fact(DisplayName = "公式の台風解析予報情報から中心位置の確度と予報円を取得できる")]
	public void TyphoonPositionAccuracy()
	{
		using var doc = OpenSample("10_04_01_170908_VPTW60.xml");
		var group = Assert.Single(doc.MeteorologicalBody.MeteorologicalInfoGroups);
		Assert.Equal(doc.MeteorologicalBody.Type, group.Type);
		var infos = group.MeteorologicalInfos.ToArray();
		var center = infos[0].MeteorologicalInfoKindProperties.Single(p => p.Type == "中心").CenterPart;
		Assert.Equal(new[] { "不確実", "不確実" }, center.Coordinates.Select(c => c.Condition));
		Assert.Equal("+12.6+146.8/", center.Coordinates.First().Value);
		Assert.Empty(center.ProbabilityCircles);
		var forecast = infos[1].MeteorologicalInfoKindProperties.Single(p => p.Type == "中心").CenterPart;
		Assert.Equal("予報円", Assert.Single(forecast.ProbabilityCircles).Type);
		Assert.Equal(forecast.ProbabilityCircle!.Value.Type, forecast.ProbabilityCircles.First().Type);
	}
}
