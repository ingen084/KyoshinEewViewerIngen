using KyoshinEewViewer.JmaXmlParser.Data;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
using System;
using System.IO;
using System.Linq;
using System.Text;
using U8Xml;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

// jmx_ib.xsd、jmx_seis.xsd、jmx_eb.xsd（2024-10-31 版）との再照合で見つかった差分。
public class SpecificationComplianceTests
{
	private static JmaXmlDocument Parse(string xml) => new(Encoding.UTF8.GetBytes(xml));

	[Theory(DisplayName = "nil属性は接頭辞によらずXMLSchema-instance名前空間に属する場合だけ適用する")]
	[InlineData("xmlns:a=\"http://www.w3.org/2001/XMLSchema-instance\"", "a:nil=\"true\"", true)]
	[InlineData("xmlns:a=\"http://www.w3.org/2001/XMLSchema-instance\"", "a:nil=\" 1 \"", true)]
	[InlineData("xmlns:a=\"http://www.w3.org/2001/XMLSchema-instance\"", "a:nil=\"0\"", false)]
	[InlineData("xmlns:xsi=\"urn:unrelated\"", "xsi:nil=\"true\"", false)]
	[InlineData("", "nil=\"true\"", false)]
	public void NilNamespaceAndBooleanLexicalValues(string declaration, string attribute, bool isNil)
	{
		var content = isNil ? "" : "2024-01-01T12:00:00+09:00";
		using var doc = Parse($"<Report {declaration}><Head><TargetDateTime {attribute}>{content}</TargetDateTime></Head></Report>");
		Assert.Equal(isNil ? (DateTimeOffset?)null : DateTimeOffset.Parse(content), doc.Head.TargetDateTime);
	}

	[Fact(DisplayName = "名前空間の再宣言でnil属性の名前空間が変われば通常の対象時刻として扱う")]
	public void NilNamespaceShadowing()
	{
		using var doc = Parse("""
			<Report xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><Head>
			  <TargetDateTime xmlns:xsi="urn:unrelated" xsi:nil="true">2024-01-01T12:00:00+09:00</TargetDateTime>
			</Head></Report>
			""");
		Assert.NotNull(doc.Head.TargetDateTime);
	}

	[Fact(DisplayName = "Periodだけがある周期別観測値と省略された震度および変化前の種別を読み取れる")]
	public void OptionalObservationFields()
	{
		using var xml = XmlParser.Parse("""
			<IntensityStation><Name>観測点</Name><Code>0123456</Code>
			  <LgIntPerPeriod Period="2.5">2</LgIntPerPeriod>
			  <SvaPerPeriod Period="2.5" unit="cm/s">50.3</SvaPerPeriod>
			</IntensityStation>
			"""u8);
		var station = new IntensityStation(xml.Root);
		Assert.Null(station.Int);
		var lg = Assert.Single(station.LgIntPerPeriods);
		Assert.Null(lg.PeriodicBand);
		Assert.Null(lg.PeriodUnit);
		Assert.Equal(2.5f, lg.Period);
		var sva = Assert.Single(station.SvaPerPeriods);
		Assert.Null(sva.PeriodicBand);
		Assert.Null(sva.PeriodUnit);
		Assert.Equal(2.5f, sva.Period);
		Assert.Equal(50.3f, sva.Value);

		using var categoryXml = XmlParser.Parse("<Category><Kind><Name>予報</Name><Code>00</Code></Kind></Category>"u8);
		var category = new Category(categoryXml.Root);
		Assert.Equal("00", category.Kind.Code);
		Assert.Null(category.LastKind);
	}

	[Fact(DisplayName = "物理量は任意の接頭辞とデフォルト名前空間で取得でき異なる名前空間の同名要素は無視する")]
	public void ElementBasisExpandedNames()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="urn:unrelated" xmlns:eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body><Earthquake>
			  <Hypocenter><Area><Name>震央</Name><Code type="震央地名">1</Code>
			    <eb:Coordinate>+35.0+140.0/</eb:Coordinate>
			  </Area></Hypocenter>
			  <jmx_eb:Magnitude type="無関係">9.9</jmx_eb:Magnitude>
			  <eb:Magnitude type="Mj">7.0</eb:Magnitude>
			  <Magnitude xmlns="http://xml.kishou.go.jp/jmaxml1/elementBasis1/" type="Mw">7.1</Magnitude>
			</Earthquake></Body></Report>
			""");
		var earthquake = doc.EarthquakeBody.Earthquake!.Value;
		Assert.Equal("7.0", earthquake.Magnitude.Value);
		Assert.Equal(new[] { "7.0", "7.1" }, earthquake.Magnitudes.Select(m => m.Value));
		Assert.Equal("+35.0+140.0/", earthquake.Hypocenter.Area.Coordinate.Value);
		Assert.Single(earthquake.Hypocenter.Area.Coordinates);
	}

	[Fact(DisplayName = "公式台風サンプルの基本要素の接頭辞を変更しても階級と予報円を取得できる")]
	public void OfficialTyphoonWithDifferentPrefix()
	{
		var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "10_04_01_170908_VPTW60.xml"));
		var renamed = original.Replace("xmlns:jmx_eb=", "xmlns:eb=").Replace("jmx_eb:", "eb:");
		using var doc = Parse(renamed);
		var infos = doc.MeteorologicalBody.MeteorologicalInfos.ToArray();
		var properties = infos[0].MeteorologicalInfoKindProperties.ToArray();
		Assert.False(string.IsNullOrEmpty(properties.Single(p => p.Type == "階級").ClassPart.TyphoonClass));
		var center = properties.Single(p => p.Type == "中心").CenterPart;
		Assert.Equal("不確実", center.Coordinates.First().Condition);
		Assert.False(string.IsNullOrEmpty(center.Pressure.Value));
		Assert.NotEmpty(center.Speeds);
		var circle = infos[1].MeteorologicalInfoKindProperties.Single(p => p.Type == "中心").CenterPart.ProbabilityCircle!.Value;
		Assert.NotEmpty(circle.BasePoints);
		Assert.NotEmpty(circle.Axes.First().Radiuses);
	}

	[Fact(DisplayName = "震源と精度情報を省略した電文をTryGetで処理し未定義の精度値をNaNとして保持する")]
	public void OptionalHypocenterAndAccuracy()
	{
		using var doc = Parse("""
			<Report xmlns:eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body>
			  <Earthquake><eb:Magnitude type="Mj">NaN</eb:Magnitude></Earthquake>
			  <Earthquake><Hypocenter><Area/></Hypocenter></Earthquake>
			  <Earthquake><Hypocenter><Area/><Accuracy>
			    <Epicenter rank="4" rank2="9">NaN</Epicenter><Depth rank="4">NaN</Depth>
			    <MagnitudeCalculation rank="4">NaN</MagnitudeCalculation><NumberOfMagnitudeCalculation>5</NumberOfMagnitudeCalculation>
			  </Accuracy></Hypocenter></Earthquake>
			</Body></Report>
			""");
		var earthquakes = doc.EarthquakeBody.Earthquakes.ToArray();
		Assert.False(earthquakes[0].TryGetHypocenter(out _));
		Assert.True(earthquakes[1].TryGetHypocenter(out var withoutAccuracy));
		Assert.False(withoutAccuracy.TryGetAccuracy(out _));
		Assert.True(earthquakes[2].TryGetHypocenter(out var hypocenter));
		Assert.True(hypocenter.TryGetAccuracy(out var accuracy));
		Assert.True(float.IsNaN(accuracy.Epicenter));
		Assert.True(float.IsNaN(accuracy.Depth));
		Assert.True(float.IsNaN(accuracy.MagnitudeCalculation));
		Assert.Equal(9, accuracy.EpicenterRank2);
	}

	[Fact(DisplayName = "共通の震度スキーマの項目を予測と観測の両方から取得できる")]
	public void SharedIntensityDetailFields()
	{
		using var xml = XmlParser.Parse("""
			<Detail><MaxInt>4</MaxInt><MaxLgInt>3</MaxLgInt><LgCategory>2</LgCategory>
			  <ForecastInt bound="以上"><From>5-</From><To>over</To></ForecastInt>
			  <ForecastLgInt><From>2</From><To>3</To></ForecastLgInt>
			  <Appendix><MaxIntChange>1</MaxIntChange><MaxLgIntChange>2</MaxLgIntChange>
			    <MaxIntChangeReason>3</MaxIntChangeReason><MaxLgIntChangeReason>9</MaxLgIntChangeReason></Appendix>
			</Detail>
			"""u8);
		var forecast = new IntensityForecast(xml.Root);
		var observation = new IntensityObservation(xml.Root);
		Assert.Equal("4", forecast.MaxInt);
		Assert.Equal(observation.MaxLgInt, forecast.MaxLgInt);
		Assert.Equal(observation.LgCategory, forecast.LgCategory);
		Assert.Equal("以上", observation.ForecastInt!.Value.Bound);
		Assert.Equal("3", observation.ForecastLgInt!.Value.To);
		var appendix = observation.Appendix!.Value;
		Assert.Equal(forecast.MaxIntChange, appendix.MaxIntChange);
		Assert.Equal(forecast.MaxLgIntChange, appendix.MaxLgIntChange);
		Assert.Equal(forecast.MaxIntChangeReason, appendix.MaxIntChangeReason);
		Assert.Equal(forecast.Appendix!.Value.MaxLgIntChangeReason, appendix.MaxLgIntChangeReason);
	}

	[Fact(DisplayName = "共通の津波スキーマの項目と命名地震および次回発表予定を取りこぼさない")]
	public void SharedTsunamiFields()
	{
		const string item = """
			<Item><Area><Name>地域</Name><Code>1</Code></Area>
			  <Category><Kind><Name>津波注意報</Name><Code>62</Code></Kind></Category>
			  <FirstHeight><Condition>到達</Condition></FirstHeight><MaxHeight><Condition>観測中</Condition></MaxHeight>
			  <Duration>PT6H</Duration><Station><Name>観測点</Name><Code>12345</Code><FirstHeight/></Station>
			</Item>
			""";
		using var doc = Parse($"<Report><Body><Naming english=\"Named earthquake\">地震名</Naming><Tsunami><Observation>{item}</Observation><Estimation>{item}</Estimation></Tsunami><NextAdvisory>次回発表</NextAdvisory></Body></Report>");
		Assert.Equal("地震名", doc.TsunamiBody.Naming);
		Assert.Equal("Named earthquake", doc.TsunamiBody.NamingEnglish);
		Assert.Equal("次回発表", doc.TsunamiBody.NextAdvisory);
		var tsunami = doc.EarthquakeBody.Tsunami!.Value;
		var observation = Assert.Single(tsunami.Observation!.Value.Items);
		var estimation = Assert.Single(tsunami.Estimation!.Value.Items);
		Assert.Equal("62", observation.Category!.Value.Kind.Code);
		Assert.Equal("到達", observation.FirstHeight!.Value.Condition);
		Assert.Equal("観測中", observation.MaxHeight!.Value.Condition);
		Assert.Equal("PT6H", observation.Duration);
		Assert.Equal(observation.Duration, estimation.Duration);
		Assert.Equal(observation.Category!.Value.Kind.Code, estimation.Category!.Value.Kind.Code);
		Assert.Equal(12345, Assert.Single(estimation.Stations).Code);

		using var noCategory = XmlParser.Parse("<Item><Area/></Item>"u8);
		Assert.False(new TsunamiForecastItem(noCategory.Root).TryGetCategory(out _));
	}

	[Fact(DisplayName = "地震関連情報では必須のtypeを検証し旧形式Tokaiでは省略できる")]
	public void RequiredEarthquakeInfoType()
	{
		using var info = XmlParser.Parse("<EarthquakeInfo><InfoKind>情報</InfoKind><Text>本文</Text></EarthquakeInfo>"u8);
		Assert.Throws<JmaXmlParseException>(() => new EarthquakeInfo(info.Root).Type);
		using var tokai = XmlParser.Parse("<Tokai><InfoKind>情報</InfoKind><Text>本文</Text></Tokai>"u8);
		Assert.Null(new EarthquakeInfo(tokai.Root).Type);
	}
}
