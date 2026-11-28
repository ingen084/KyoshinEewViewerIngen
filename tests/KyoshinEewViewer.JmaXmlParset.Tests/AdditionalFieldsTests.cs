using KyoshinEewViewer.JmaXmlParser.Data;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
using System;
using System.Linq;
using System.Text;
using U8Xml;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

// 公式スキーマ（2024-10-31 版）の任意項目・繰り返し要素を検証する。
// https://xml.kishou.go.jp/jmaxml_20241031_Schema%28xsd%29.zip
public class AdditionalFieldsTests
{
	private static JmaXmlDocument Parse(string body)
		=> new(Encoding.UTF8.GetBytes(body));

	[Theory(DisplayName = "取消電文で対象時刻がxsi:nilの場合はnullを返し有効な時刻は保持する")]
	[InlineData("<TargetDateTime xsi:nil=\"true\"/>", null)]
	[InlineData("<TargetDateTime xsi:nil=\"1\"/>", null)]
	[InlineData("<TargetDateTime xsi:nil=\"false\">2024-01-01T12:00:00+09:00</TargetDateTime>", "2024-01-01T12:00:00+09:00")]
	public void NullableTargetTime(string element, string? expected)
	{
		using var doc = Parse($"<Report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><Head>{element}</Head></Report>");
		Assert.Equal(expected == null ? (DateTimeOffset?)null : DateTimeOffset.Parse(expected), doc.Head.TargetDateTime);
	}

	[Fact(DisplayName = "地震名の英語表記と複数の地震およびマグニチュードを既存の単件APIと併用できる")]
	public void MultipleEarthquakesAndMagnitudes()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/">
			<Body>
			  <Naming english="Named earthquake">命名地震</Naming>
			  <Earthquake><ArrivalTime>2024-01-01T12:00:00+09:00</ArrivalTime>
			    <Hypocenter><Area><Name>遠地</Name><Code type="震央地名">999</Code>
			      <jmx_eb:Coordinate description="震源位置">+1.0+2.0/</jmx_eb:Coordinate>
			      <DetailedCode type="詳細震央地名">1234</DetailedCode>
			    </Area><Source>USGS</Source></Hypocenter>
			    <jmx_eb:Magnitude type="Mj">7.0</jmx_eb:Magnitude>
			    <jmx_eb:Magnitude type="Mw">7.1</jmx_eb:Magnitude>
			  </Earthquake>
			  <Earthquake><jmx_eb:Magnitude type="Mj">6.0</jmx_eb:Magnitude></Earthquake>
			</Body></Report>
			""");
		var body = doc.EarthquakeBody;
		Assert.Equal("命名地震", body.Naming);
		Assert.Equal("Named earthquake", body.NamingEnglish);
		Assert.Equal(body.NamingEnglish, body.ForecastCommentText);
		Assert.Equal(2, body.Earthquakes.Count());
		var earthquake = body.Earthquake!.Value;
		Assert.Equal(new[] { "Mj", "Mw" }, earthquake.Magnitudes.Select(m => m.Type));
		Assert.Equal("7.0", earthquake.Magnitude.Value);
		Assert.Equal("USGS", earthquake.Hypocenter.Source);
		Assert.Null(earthquake.Hypocenter.Area.Source);
		Assert.Equal("震央地名", earthquake.Hypocenter.Area.CodeType);
		Assert.Equal("詳細震央地名", earthquake.Hypocenter.Area.DetailedCodeType);
		Assert.Null(earthquake.Hypocenter.Area.ReduceCodeType);
		Assert.Null(earthquake.Hypocenter.Area.DistanceUnit);
	}

	[Fact(DisplayName = "府県と市町村の予測範囲を取得し未入電の観測区域の任意項目はnullになる")]
	public void ForecastRangesAtEachLevel()
	{
		using var xml = XmlParser.Parse("""
			<Pref><Name>県</Name><Code>1</Code>
			  <ForecastInt bound="以上"><From>5-</From><To>over</To></ForecastInt>
			  <ForecastLgInt><From>3</From><To>4</To></ForecastLgInt>
			  <ArrivalTime>2024-01-01T12:00:30+09:00</ArrivalTime><Condition>主要動到達</Condition>
			  <Area><Name>地域</Name><Code>2</Code><Condition>震度５弱以上未入電</Condition>
			    <City><Name>市</Name><Code>3</Code>
			      <Category><Kind><Name>警報</Name><Code>11</Code></Kind></Category>
			      <ForecastInt><From>5+</From><To>6-</To></ForecastInt>
			      <ForecastLgInt><From>2</From><To>3</To></ForecastLgInt>
			      <ArrivalTime>2024-01-01T12:00:40+09:00</ArrivalTime>
			    </City>
			  </Area>
			</Pref>
			"""u8);
		var pref = new Pref(xml.Root);
		Assert.Equal("以上", pref.ForecastInt!.Value.Bound);
		Assert.Equal("over", pref.ForecastInt!.Value.To);
		Assert.Equal("3", pref.ForecastLgInt!.Value.From);
		Assert.Equal(30, pref.ArrivalTime!.Value.Second);
		Assert.Equal("主要動到達", pref.Condition);
		Assert.Null(pref.Category);
		var area = Assert.Single(pref.Areas);
		Assert.Equal("震度５弱以上未入電", area.Condition);
		Assert.Null(area.Category);
		Assert.Null(area.ForecastInt);
		Assert.Null(area.ForecastLgInt);
		Assert.Null(area.ArrivalTime);
		var city = Assert.Single(area.Cities);
		Assert.Equal("11", city.Category!.Value.Kind.Code);
		Assert.Equal("5+", city.ForecastInt!.Value.From);
		Assert.Equal("3", city.ForecastLgInt!.Value.To);
		Assert.Equal(40, city.ArrivalTime!.Value.Second);
	}

	[Fact(DisplayName = "観測点の計測震度とPeriod属性を持つ周期別データを読み取れる")]
	public void InstrumentalIntensityAndPeriodAttributes()
	{
		using var xml = XmlParser.Parse("""
			<IntensityStation><Name>観測点</Name><Code>0000001</Code><Int>4</Int><K>4.2</K><LgInt>2</LgInt>
			  <LgIntPerPeriod Period="2.5" PeriodUnit="秒">2</LgIntPerPeriod>
			  <SvaPerPeriod Period="2.5" PeriodUnit="秒" unit="cm/s">50.3</SvaPerPeriod>
			</IntensityStation>
			"""u8);
		var station = new IntensityStation(xml.Root);
		Assert.Equal("0000001", station.Code);
		Assert.Equal(4.2f, station.K);
		Assert.Equal("2", station.LgInt);
		Assert.Equal(2.5f, Assert.Single(station.LgIntPerPeriods).Period);
		Assert.Equal(2.5f, Assert.Single(station.SvaPerPeriods).Period);
		Assert.Equal(50.3f, Assert.Single(station.SvaPerPeriods).Value);
	}

	[Fact(DisplayName = "津波の時刻範囲と高さの範囲および現在の高さを区別して読み取れる")]
	public void TsunamiRangesAndCurrentHeight()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body><Tsunami>
			  <Release>解除</Release><Forecast><Item>
			    <Area><Name>予報区</Name><Code>100</Code><City><Name>市</Name><Code>01234</Code></City></Area>
			    <Category><Kind><Name>津波予報</Name><Code>71</Code></Kind></Category>
			    <FirstHeight>
			      <ArrivalTimeFrom>2024-01-01T12:00:00+09:00</ArrivalTimeFrom>
			      <ArrivalTimeTo>2024-01-01T12:10:00+09:00</ArrivalTimeTo>
			      <jmx_eb:TsunamiHeight type="第１波の高さ" unit="m">0.3</jmx_eb:TsunamiHeight><Period>5.5</Period>
			    </FirstHeight>
			    <MaxHeight>
			      <TsunamiHeightFrom type="津波の高さ" unit="m">0.2</TsunamiHeightFrom>
			      <TsunamiHeightTo type="津波の高さ" unit="m">1.0</TsunamiHeightTo><Period>8.5</Period>
			    </MaxHeight><Duration>PT6H</Duration>
			    <Station><Name>観測点</Name><Code>12345</Code><FirstHeight/>
			      <CurrentHeight><StartTime>2024-01-01T12:00:00+09:00</StartTime>
			        <EndTime>2024-01-01T12:10:00+09:00</EndTime><Condition>上昇中</Condition>
			        <jmx_eb:TsunamiHeight type="津波の高さ" unit="m">0.5</jmx_eb:TsunamiHeight>
			      </CurrentHeight>
			    </Station>
			  </Item></Forecast>
			</Tsunami></Body></Report>
			""");
		var tsunami = doc.TsunamiBody.Tsunami!.Value;
		Assert.Equal("解除", tsunami.Release);
		Assert.Null(tsunami.Estimation);
		var item = Assert.Single(tsunami.Forecast!.Value.Items);
		Assert.Equal("PT6H", item.Duration);
		Assert.Equal("01234", Assert.Single(item.Area.Cities).Code);
		var first = item.FirstHeight!.Value;
		Assert.Equal(TimeSpan.FromMinutes(10), first.ArrivalTimeTo - first.ArrivalTimeFrom);
		Assert.Null(first.ArrivalTime);
		Assert.Equal("0.3", first.TsunamiHeight!.Value.Value);
		Assert.Equal(5.5f, first.Period);
		var max = item.MaxHeight!.Value;
		Assert.Equal("0.2", max.TsunamiHeightFrom!.Value.Value);
		Assert.Equal("1.0", max.TsunamiHeightTo!.Value.Value);
		Assert.Null(max.TsunamiHeight);
		Assert.Equal(8.5f, max.Period);
		var current = Assert.Single(item.Stations).CurrentHeight!.Value;
		Assert.Equal(TimeSpan.FromMinutes(10), current.EndTime - current.StartTime);
		Assert.Equal("上昇中", current.Condition);
		Assert.Equal("0.5", current.TsunamiHeight!.Value.Value);
	}

	[Fact(DisplayName = "観測付加文のコード体系と本文および未記載の付加文を区別できる")]
	public void ObservationCommentAndOptionalComments()
	{
		using var doc = Parse("""
			<Report><Body><Comments>
			  <ObservationComment codeType="固定付加文"><Text>観測に関する情報</Text><Code>1 2</Code></ObservationComment>
			  <ForecastComment codeType="固定付加文"><Text>予測</Text><Code>3</Code></ForecastComment>
			  <VarComment codeType="固定付加文"><Text>その他</Text><Code>4</Code></VarComment>
			  <URI>https://www.jma.go.jp/</URI>
			</Comments></Body></Report>
			""");
		var comments = doc.EarthquakeBody.Comments!.Value;
		Assert.Equal("観測に関する情報", comments.ObservationCommentText);
		Assert.Equal("1 2", comments.ObservationCommentCode);
		Assert.Equal("固定付加文", comments.ObservationCommentCodeType);
		Assert.Equal("固定付加文", comments.ForecastCommentCodeType);
		Assert.Equal("固定付加文", comments.VarCommentCodeType);
		Assert.Null(comments.WarningCommentCodeType);
		Assert.Null(comments.WarningCommentText);
	}

	[Fact(DisplayName = "取消電文では追加した地震情報と津波情報の任意項目が出現しない")]
	public void CancelledReport()
	{
		using var doc = Parse("<Report><Body><Text>取消します</Text></Body></Report>");
		var body = doc.EarthquakeBody;
		Assert.Null(body.EarthquakeInfo);
		Assert.Null(body.Tokai);
		Assert.Null(body.Aftershock);
		Assert.Null(body.NamingEnglish);
		Assert.Empty(body.Earthquakes);
		Assert.Null(doc.TsunamiBody.Tsunami);
		Assert.Empty(doc.MeteorologicalBody.MeteorologicalInfoGroups);
	}

	[Fact(DisplayName = "地震回数の補足情報と旧形式の関連情報および余震発生確率を読み取れる")]
	public void CountsAndLegacyEarthquakeInformation()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body>
			  <Tokai><InfoKind>東海地震観測情報</InfoKind><Text>本文</Text></Tokai>
			  <EarthquakeCount><Item type="地震回数">
			    <StartTime>2024-01-01T12:00:00+09:00</StartTime><EndTime>2024-01-01T13:00:00+09:00</EndTime>
			    <Number>-1</Number><FeltNumber>8</FeltNumber><Condition>補足</Condition>
			  </Item></EarthquakeCount>
			  <Aftershock><Item>
			    <StartTime>2024-01-01T12:00:00+09:00</StartTime><EndTime>2024-01-02T12:00:00+09:00</EndTime>
			    <jmx_eb:ProbabilityOfAftershock type="余震発生確率" unit="%">20</jmx_eb:ProbabilityOfAftershock>
			    <TargetMagnitude type="Mj">6.0</TargetMagnitude><Text>期間別補足</Text>
			  </Item><Text>全体補足</Text></Aftershock>
			  <Intensity><Forecast><Appendix><MaxIntChange>0</MaxIntChange>
			    <MaxIntChangeReason>0</MaxIntChangeReason><MaxLgIntChangeReason>9</MaxLgIntChangeReason>
			  </Appendix></Forecast></Intensity>
			</Body></Report>
			""");
		var body = doc.EarthquakeBody;
		var count = Assert.Single(body.EarthquakeCount!.Value.Items);
		Assert.Equal("補足", count.Condition);
		Assert.Equal("-1", count.Number);
		Assert.Equal("8", count.FeltNumber);
		Assert.Equal(count.Number, Assert.Single(body.EarthquakeCount!.Value.Cities).Number);
		var tokai = body.Tokai!.Value;
		Assert.Null(tokai.Type);
		Assert.Equal("東海地震観測情報", tokai.InfoKind);
		Assert.Equal("本文", tokai.Text);
		var aftershock = body.Aftershock!.Value;
		var item = Assert.Single(aftershock.Items);
		Assert.Equal(TimeSpan.FromDays(1), item.EndTime - item.StartTime);
		Assert.True(item.ProbabilityOfAftershock.TryGetIntValue(out var probability));
		Assert.Equal(20, probability);
		Assert.Equal("6.0", item.TargetMagnitude.Value);
		Assert.Equal("期間別補足", item.Text);
		Assert.Equal("全体補足", aftershock.Text);
		Assert.Equal(9, body.Intensity!.Value.Forecast!.Value.MaxLgIntChangeReason);
	}

	[Fact(DisplayName = "気象情報の複数グループと複数項目を取得し各部品の時刻および楕円の軸を保持できる")]
	public void MeteorologicalGroupsAndParts()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body><Notice>注意事項</Notice>
			  <MeteorologicalInfos type="解析"><MeteorologicalInfo type="実況">
			    <DateTime significant="yyyy-mm-ddThh:mm" precision="PT1M" dubious="頃" description="正午頃">2024-01-01T12:00:00+09:00</DateTime><Duration>PT1H</Duration><Name>対象期間</Name>
			    <Item><Kind><Property><Type>呼称</Type><TyphoonNamePart>
			      <Name>台風名</Name><Time>2024-01-01T12:00:00+09:00</Time>
			    </TyphoonNamePart></Property></Kind></Item>
			    <Item><Kind><Property><Type>階級</Type><ClassPart>
			      <Time>2024-01-01T12:00:00+09:00</Time><Remark>階級の補足</Remark>
			    </ClassPart></Property><Property><Type>中心</Type><CenterPart>
			      <ProbabilityCircle type="予報円"><jmx_eb:Axes>
			        <jmx_eb:LongAxis><jmx_eb:Bearings type="方位角" unit="度">90</jmx_eb:Bearings>
			          <jmx_eb:Radius type="半径" unit="km">100</jmx_eb:Radius></jmx_eb:LongAxis>
			        <jmx_eb:ShortAxis><jmx_eb:Direction type="方向">北</jmx_eb:Direction>
			          <jmx_eb:Radius type="半径" unit="km">50</jmx_eb:Radius></jmx_eb:ShortAxis>
			      </jmx_eb:Axes></ProbabilityCircle>
			      <ProbabilityCircle type="予報円"/>
			      <jmx_eb:Direction type="移動方向" unit="度">90</jmx_eb:Direction>
			      <jmx_eb:Direction type="移動方向" unit="１６方位漢字">東</jmx_eb:Direction>
			      <jmx_eb:Radius type="半径" unit="km">100</jmx_eb:Radius>
			      <Time>2024-01-01T12:00:00+09:00</Time><Remark>中心の補足</Remark>
			    </CenterPart></Property><Property><Type>風</Type><WindPart>
			      <Time>2024-01-01T12:00:00+09:00</Time><Remark>風の補足</Remark>
			    </WindPart></Property></Kind></Item>
			  </MeteorologicalInfo></MeteorologicalInfos>
			  <MeteorologicalInfos type="予報"><MeteorologicalInfo><DateTime type="予報">2024-01-01T13:00:00+09:00</DateTime><Item/></MeteorologicalInfo></MeteorologicalInfos>
			</Body></Report>
			""");
		Assert.Equal("注意事項", Assert.Single(doc.MeteorologicalBody.Notices));
		var groups = doc.MeteorologicalBody.MeteorologicalInfoGroups.ToArray();
		Assert.Equal(new[] { "解析", "予報" }, groups.Select(g => g.Type));
		Assert.Single(doc.MeteorologicalBody.MeteorologicalInfos);
		var info = Assert.Single(groups[0].MeteorologicalInfos);
		Assert.Equal("実況", info.Type);
		Assert.Equal("PT1H", info.Duration);
		Assert.Equal("対象期間", info.Name);
		Assert.Null(info.DateTimeType);
		Assert.Equal("yyyy-mm-ddThh:mm", info.DateTimeSignificant);
		Assert.Equal("PT1M", info.DateTimePrecision);
		Assert.Equal("頃", info.DateTimeDubious);
		Assert.Equal("正午頃", info.DateTimeDescription);
		var properties = info.MeteorologicalInfoKindProperties.ToArray();
		Assert.Equal(4, properties.Length);
		Assert.Equal(info.DateTime, properties[0].TyphoonNamePart.Time);
		Assert.Equal(info.DateTime, properties[1].ClassPart.Time);
		Assert.Equal("階級の補足", properties[1].ClassPart.Remark);
		var center = properties[2].CenterPart;
		Assert.Equal(info.DateTime, center.Time);
		Assert.Equal("中心の補足", center.Remark);
		Assert.Equal(2, center.ProbabilityCircles.Count());
		Assert.Equal(new[] { "90", "東" }, center.Directions.Select(d => d.Value));
		Assert.Equal("100", Assert.Single(center.Radiuses).Value);
		var circle = center.ProbabilityCircles.First();
		Assert.Equal("90", Assert.Single(Assert.Single(circle.LongAxes).Bearings).Value);
		Assert.Equal("北", Assert.Single(Assert.Single(circle.ShortAxes).Directions).Value);
		Assert.Equal(info.DateTime, properties[3].WindPart.Time);
		Assert.Equal("風の補足", properties[3].WindPart.Remark);
		Assert.Equal("予報", Assert.Single(groups[1].MeteorologicalInfos).DateTimeType);
	}

	[Fact(DisplayName = "見出しの対象地域をコード省略の座標と線および多角形として取得できる")]
	public void HeadlineGeometry()
	{
		using var doc = Parse("""
			<Report xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Head><Headline><Text/>
			  <Information type="対象地域"><Item><Kind><Name>情報</Name></Kind><Areas codeType="領域">
			    <Area><Name>対象領域</Name>
			      <jmx_eb:Circle type="円"><jmx_eb:BasePoint>+35.0+140.0/</jmx_eb:BasePoint></jmx_eb:Circle>
			      <jmx_eb:Coordinate condition="不確実">+35.0+140.0/</jmx_eb:Coordinate>
			      <jmx_eb:Line>+35.0+140.0/+36.0+141.0/</jmx_eb:Line>
			      <jmx_eb:Polygon>+35.0+140.0/+36.0+141.0/+35.0+142.0/+35.0+140.0/</jmx_eb:Polygon>
			    </Area>
			  </Areas></Item></Information>
			</Headline></Head></Report>
			""");
		var area = Assert.Single(Assert.Single(Assert.Single(doc.Head.Headline.Informations).Items).Areas);
		Assert.Null(area.Code);
		Assert.Equal("不確実", Assert.Single(area.Coordinates).Condition);
		Assert.Null(Assert.Single(area.Coordinates).Description);
		Assert.Empty(Assert.Single(area.Circles).Axes);
		Assert.Equal("+35.0+140.0/", Assert.Single(Assert.Single(area.Circles).BasePoints).Value);
		Assert.Equal("+35.0+140.0/+36.0+141.0/", Assert.Single(area.Lines).Value);
		Assert.Equal("+35.0+140.0/+36.0+141.0/+35.0+142.0/+35.0+140.0/", Assert.Single(area.Polygons).Value);
	}

	[Fact(DisplayName = "必須フィールドを欠く地震関連情報と余震情報は取得時にパース例外になる")]
	public void MissingRequiredFields()
	{
		using var xml = XmlParser.Parse("<Root/>"u8);
		Assert.Throws<JmaXmlParseException>(() => new EarthquakeInfo(xml.Root).InfoKind);
		Assert.Throws<JmaXmlParseException>(() => new EarthquakeInfo(xml.Root).Text);
		Assert.Throws<JmaXmlParseException>(() => new InfoSerial(xml.Root).CodeType);
		Assert.Throws<JmaXmlParseException>(() => new InfoSerial(xml.Root).Name);
		Assert.Throws<JmaXmlParseException>(() => new InfoSerial(xml.Root).Code);
		Assert.Throws<JmaXmlParseException>(() => new AftershockItem(xml.Root).StartTime);
		Assert.Throws<JmaXmlParseException>(() => new AftershockItem(xml.Root).ProbabilityOfAftershock);
		Assert.Throws<JmaXmlParseException>(() => new TsunamiEstimationItem(xml.Root).Area);
	}
}
