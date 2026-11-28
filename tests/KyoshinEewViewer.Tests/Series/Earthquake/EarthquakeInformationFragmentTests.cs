using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using System.Text;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

/// <summary>
/// 地震情報の電文から地震情報の断片を作る処理を検証する
/// </summary>
public class EarthquakeInformationFragmentTests
{
	private sealed class TestTelegram() : Telegram("key", "title", "TEST", DateTime.UnixEpoch)
	{
		public override Task<Stream> GetBodyAsync() => throw new NotSupportedException();
		public override void Cleanup() { }
	}

	private static EarthquakeInformationFragment Create(string xml)
		=> EarthquakeInformationFragment.CreateFromJmxXmlDocument(new TestTelegram(), JmaXml.Read<EarthquakeFragmentView>(Encoding.UTF8.GetBytes(xml)));

	private static string Report(string title, string body, string status = "通常", string headTitle = "見出し", string infoType = "発表")
		=> $"""
			<?xml version="1.0" encoding="UTF-8"?>
			<Report xmlns="http://xml.kishou.go.jp/jmaxml1/" xmlns:jmx="http://xml.kishou.go.jp/jmaxml1/">
				<Control>
					<Title>{title}</Title>
					<DateTime>2026-01-01T03:05:10Z</DateTime>
					<Status>{status}</Status>
					<EditorialOffice>気象庁本庁</EditorialOffice>
					<PublishingOffice>気象庁</PublishingOffice>
				</Control>
				<Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
					<Title>{headTitle}</Title>
					<ReportDateTime>2026-01-01T12:05:00+09:00</ReportDateTime>
					<TargetDateTime>2026-01-01T12:02:00+09:00</TargetDateTime>
					<EventID>20260101120000</EventID>
					<InfoType>{infoType}</InfoType>
					<Headline>
						<Text>見出し文</Text>
						<Information type="震度速報">
							<Item><Kind><Name>震度４</Name></Kind><Areas codeType="地震情報／細分区域"><Area><Name>見出しの地域</Name><Code>390</Code></Area></Areas></Item>
						</Information>
					</Headline>
				</Head>
				<Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/seismology1/" xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/">
					{body}
				</Body>
			</Report>
			""";

	private const string Earthquake = """
		<Earthquake>
			<OriginTime>2026-01-01T12:00:00+09:00</OriginTime>
			<ArrivalTime>2026-01-01T12:00:00+09:00</ArrivalTime>
			<Hypocenter>
				<Area>
					<Name>能登半島沖</Name>
					<Code type="震央地名">495</Code>
					<jmx_eb:Coordinate description="北緯３７．５度　東経１３７．３度　深さ　１０ｋｍ" datum="日本測地系">+37.5+137.3-10000/</jmx_eb:Coordinate>
				</Area>
			</Hypocenter>
			<jmx_eb:Magnitude type="Mj" description="Ｍ５．１">5.1</jmx_eb:Magnitude>
		</Earthquake>
		""";

	private const string Intensity = """
		<Intensity>
			<Observation>
				<CodeDefine><Type xpath="Pref/Code">地震情報／都道府県等</Type></CodeDefine>
				<MaxInt>5-</MaxInt>
				<MaxLgInt>2</MaxLgInt>
				<Pref>
					<Name>石川県</Name><Code>17</Code><MaxInt>5-</MaxInt>
					<Area>
						<Name>石川県能登</Name><Code>390</Code><MaxInt>5-</MaxInt>
						<City>
							<Name>輪島市</Name><Code>1720400</Code><MaxInt>5-</MaxInt>
							<IntensityStation><Name>輪島市鳳至町</Name><Code>1720400</Code><Int>5-</Int></IntensityStation>
						</City>
					</Area>
					<Area><Name>石川県加賀</Name><Code>391</Code><MaxInt>3</MaxInt></Area>
				</Pref>
				<Pref>
					<Name>富山県</Name><Code>16</Code><MaxInt>3</MaxInt>
					<Area><Name>富山県東部</Name><Code>380</Code><MaxInt>3</MaxInt></Area>
				</Pref>
			</Observation>
		</Intensity>
		""";

	private const string Comments = """
		<Comments>
			<ForecastComment codeType="固定付加文"><Text>この地震による津波の心配はありません。</Text><Code>0215</Code></ForecastComment>
			<FreeFormComment>自由形式の文</FreeFormComment>
		</Comments>
		""";

	[Fact(DisplayName = "震度速報から最大震度と代表地域を読み取り、地域が複数あることを判定できる")]
	public void IntensityReport()
	{
		var fragment = Assert.IsType<IntensityInformationFragment>(Create(Report("震度速報", Intensity + Comments, status: "訓練")));

		Assert.Equal("震度速報", fragment.Title);
		Assert.True(fragment.IsTraining);
		Assert.False(fragment.IsTest);
		Assert.Equal(new DateTime(2026, 1, 1, 12, 5, 0), fragment.ArrivedTime);
		Assert.Equal(new DateTime(2026, 1, 1, 12, 2, 0), fragment.DetectionTime);
		Assert.Equal("石川県能登", fragment.Place);
		Assert.Equal(JmaIntensity.Int5Lower, fragment.MaxIntensity);
		Assert.False(fragment.IsOnlypoint);
		Assert.Equal("この地震による津波の心配はありません。", fragment.Comment);
		Assert.Equal("自由形式の文", fragment.FreeFormComment);

		var single = Assert.IsType<IntensityInformationFragment>(Create(Report("震度速報", """
			<Intensity><Observation><MaxInt>3</MaxInt><Pref><Name>石川県</Name><Area><Name>石川県能登</Name></Area></Pref></Observation></Intensity>
			""", status: "試験")));
		Assert.True(single.IsOnlypoint);
		Assert.True(single.IsTest);
		Assert.Null(single.Comment);
	}

	[Fact(DisplayName = "震源に関する情報と震源・震度に関する情報から震源要素を読み取れる")]
	public void HypocenterReports()
	{
		var hypocenter = Create(Report("震源に関する情報", Earthquake + Comments));
		Assert.Equal(typeof(HypocenterInformationFragment), hypocenter.GetType());

		var fragment = Assert.IsType<HypocenterAndIntensityInformationFragment>(Create(Report("震源・震度に関する情報", Earthquake + Intensity + Comments)));
		foreach (var f in new[] { (HypocenterInformationFragment)hypocenter, fragment })
		{
			Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), f.OccurrenceTime);
			Assert.Equal("能登半島沖", f.Place);
			Assert.Equal(37.5f, f.Location.Latitude);
			Assert.Equal(137.3f, f.Location.Longitude);
			Assert.Equal(10, f.Depth);
			Assert.Equal(10, f.DepthError);
			Assert.Equal(5.1f, f.Magnitude);
			Assert.Null(f.MagnitudeAlternativeText);
			Assert.Equal("この地震による津波の心配はありません。", f.Comment);
		}
		Assert.Equal(JmaIntensity.Int5Lower, fragment.MaxIntensity);
		Assert.False(fragment.IsForeign);
		Assert.False(fragment.IsVolcano);
	}

	[Fact(DisplayName = "度分形式の震源位置の優先・マグニチュード不明・遠地地震・大規模な噴火を読み取れる")]
	public void HypocenterVariations()
	{
		var fragment = Assert.IsType<HypocenterAndIntensityInformationFragment>(Create(Report("震源・震度に関する情報", """
			<Earthquake>
				<OriginTime>2026-01-01T12:00:00+09:00</OriginTime>
				<Hypocenter>
					<Area>
						<Name>南太平洋</Name>
						<jmx_eb:Coordinate description="南緯２０．３度　西経１７５．２度　深さ不明" datum="日本測地系">-20.3-175.2/</jmx_eb:Coordinate>
						<jmx_eb:Coordinate type="震源位置（度分）" description="南緯２０度１８分　西経１７５度１２分　深さ　２０ｋｍ">-2018-17512-20000/</jmx_eb:Coordinate>
					</Area>
				</Hypocenter>
				<jmx_eb:Magnitude type="Mj" condition="不明" description="Ｍ不明">NaN</jmx_eb:Magnitude>
			</Earthquake>
			<Comments>
				<FreeFormComment>１５日１３時１０分頃（日本時間）にフンガ・トンガ－フンガ・ハアパイ火山で大規模な噴火が発生しました。</FreeFormComment>
			</Comments>
			""", headTitle: "遠地地震に関する情報")));

		Assert.Equal(-20.3f, fragment.Location.Latitude, 3);
		Assert.Equal(-175.2f, fragment.Location.Longitude, 3);
		Assert.Equal(20, fragment.Depth);
		Assert.Equal(1, fragment.DepthError);
		Assert.True(float.IsNaN(fragment.Magnitude));
		Assert.Equal("Ｍ不明", fragment.MagnitudeAlternativeText);
		Assert.Equal(JmaIntensity.Unknown, fragment.MaxIntensity);
		Assert.True(fragment.IsForeign);
		Assert.True(fragment.IsVolcano);
		Assert.Equal("フンガ・トンガ－フンガ・ハアパイ火山", fragment.VolcanoName);
		Assert.Null(fragment.Comment);
	}

	[Fact(DisplayName = "長周期地震動に関する観測情報から長周期地震動階級を読み取れる")]
	public void LpgmReport()
	{
		var fragment = Assert.IsType<LpgmIntensityInformationFragment>(Create(Report("長周期地震動に関する観測情報", Earthquake + Intensity)));

		Assert.Equal(JmaIntensity.Int5Lower, fragment.MaxIntensity);
		Assert.Equal(LpgmIntensity.LpgmInt2, fragment.MaxLpgmIntensity);
		Assert.Equal("能登半島沖", fragment.Place);
	}

	[Fact(DisplayName = "必要な項目が欠けている電文や対象外の電文は例外になる")]
	public void InvalidReports()
	{
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("震源に関する情報", Comments)));
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("震源・震度に関する情報", Earthquake.Replace(">5.1<", "><"))));
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("震源・震度に関する情報", Earthquake.Replace("OriginTime", "Other"))));
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("震源・震度に関する情報", Earthquake.Replace("jmx_eb:Coordinate", "Other"))));
		Assert.Throws<EarthquakeWatchException>(() => Create(Report("震度速報", Comments)));
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("震度速報", Intensity.Replace("<MaxInt>5-</MaxInt>", ""))));
		Assert.Throws<EarthquakeInformationFragmentProcessException>(() => Create(Report("津波警報・注意報・予報a", Earthquake)));
		Assert.Throws<JmaXmlParseException>(() => Create("<Report><Head><Title>見出し</Title></Head></Report>"));
		Assert.Throws<JmaXmlParseException>(() => Create("<Other/>"));
		Assert.Throws<FormatException>(() => Create("<Report><Control><Title>震度速報</Control></Report>"));
	}

	[Fact(DisplayName = "取消の電文は同じ種類の情報を取り消し、訂正の電文は直前の情報を訂正済みにする")]
	public void CancelAndCorrection()
	{
		static JmaXmlDocument<EarthquakeFragmentView> Read(string xml) => JmaXml.Read<EarthquakeFragmentView>(Encoding.UTF8.GetBytes(xml));

		var evt = new EarthquakeEvent("20260101120000");
		var first = evt.ProcessTelegram(new KeyedTelegram("1"), Read(Report("震度速報", Intensity)));
		var corrected = evt.ProcessTelegram(new KeyedTelegram("2"), Read(Report("震度速報", Intensity, infoType: "訂正")));

		Assert.NotNull(first);
		Assert.NotNull(corrected);
		Assert.True(first.IsCorrected);
		Assert.False(corrected.IsCorrected);

		Assert.Null(evt.ProcessTelegram(new KeyedTelegram("3"), Read(Report("震度速報", "<Text>取り消します。</Text>", infoType: "取消"))));
		Assert.All(evt.Fragments, f => Assert.True(f.IsCancelled));
		Assert.True(evt.IsCancelled);
	}

	private sealed class KeyedTelegram(string key) : Telegram(key, "title", "TEST", DateTime.UnixEpoch)
	{
		public override Task<Stream> GetBodyAsync() => throw new NotSupportedException();
		public override void Cleanup() { }
	}
}
