using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using System;
using System.Linq;
using System.Text;
using U8Xml;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

public class WeatherWarningBehaviorTests
{
	private static JmaXmlDocument Parse(string body) => new(Encoding.UTF8.GetBytes(body));

	[Fact(DisplayName = "発表なしの種別省略と今後の切替予定および未知の種別コードを保持する")]
	public void NoWarningAndFutureTransition()
	{
		using var doc = Parse("""
			<Report><Body><Warning type="気象警報・注意報（市町村等）">
			  <Item><Kind><Status>発表警報・注意報はなし</Status></Kind><Area><Name>市</Name><Code>0123400</Code></Area></Item>
			  <Item><Kind><Name>将来の警報</Name><Code>99</Code><Status>継続</Status><Condition>特記事項</Condition>
			    <LastKind><Name>注意報</Name><Code>10</Code></LastKind>
			    <NextKinds><NextKind><Name>警報</Name><Code>03</Code><Condition>切替予定</Condition>
			      <DateTime type="切替予定時刻">2026-11-09T18:00:00+09:00</DateTime><Sentence>９日夕方から警報級</Sentence>
			    </NextKind></NextKinds><Attention><Note>警戒事項</Note></Attention><Addition><Note>付加事項</Note></Addition>
			    <WarningNotice><StartTime><Date>９日</Date><Term>夕方</Term></StartTime><Note>警報への切替を予告</Note></WarningNotice>
			  </Kind><Area><Name>町</Name><Code>0123500</Code></Area><ChangeStatus>警報・注意報種別に変化有</ChangeStatus><FullStatus>一部</FullStatus><EditingMark>0</EditingMark></Item>
			</Warning></Body></Report>
			""");
		var items = Assert.Single(doc.MeteorologicalBody.Warnings).Items.ToArray();
		var none = Assert.Single(items[0].Kinds);
		Assert.Null(none.Name);
		Assert.Null(none.Code);
		Assert.Null(none.LastKind);
		Assert.Empty(none.Properties);
		Assert.Equal("発表警報・注意報はなし", none.Status);
		Assert.Equal("0123400", items[0].Area!.Value.Code);
		var kind = Assert.Single(items[1].Kinds);
		Assert.Equal("99", kind.Code);
		Assert.Equal("10", kind.LastKind!.Value.Code);
		Assert.Equal("警戒事項", Assert.Single(kind.AttentionNotes));
		Assert.Equal("付加事項", Assert.Single(kind.AdditionNotes));
		var next = Assert.Single(kind.NextKinds);
		Assert.Equal("03", next.Code);
		Assert.Equal(TimeSpan.FromHours(9), next.DateTime!.Value.Offset);
		Assert.Equal("切替予定時刻", next.DateTimeType);
		Assert.Equal("９日夕方から警報級", next.Sentence);
		Assert.Equal("夕方", kind.WarningNotice!.Value.StartTime.Term);
		Assert.Equal("警報への切替を予告", kind.WarningNotice!.Value.Note);
	}

	[Fact(DisplayName = "時系列の危険度と物理量の参照番号を時刻定義に対応付けられる")]
	public void TimeSeriesReferenceNumbers()
	{
		using var doc = Parse("""
			<Report xmlns:eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/" xmlns:fake="urn:other"><Body>
			  <MeteorologicalInfos type="気象警報・注意報時系列情報"><TimeSeriesInfo><TimeDefines>
			    <TimeDefine timeId="1"><DateTime>2026-11-09T12:00:00+09:00</DateTime><Duration>PT3H</Duration><Name>９日昼過ぎ</Name></TimeDefine>
			    <TimeDefine timeId="2"><DateTime>2026-11-09T15:00:00+09:00</DateTime><Duration>PT3H</Duration><Name>９日夕方</Name></TimeDefine>
			  </TimeDefines><Item><Kind><Status>発表</Status><DateTime type="発表時刻">2026-11-09T11:00:00+09:00</DateTime>
			    <Property><Type>雨</Type><PrecipitationPart refID="1"><Base>
			      <fake:Precipitation type="異なる名前空間">999</fake:Precipitation>
			      <eb:Precipitation type="１時間最大雨量" unit="mm" refID="2" condition="以上" description="５０ミリ以上">50</eb:Precipitation>
			    </Base></PrecipitationPart><SignificancyPart><Base>
			      <Significancy type="大雨危険度" refID="1"><Name>警報級</Name><Code>31</Code></Significancy>
			    </Base></SignificancyPart></Property>
			  </Kind><Area><Name>市</Name><Code>0123400</Code></Area></Item></TimeSeriesInfo></MeteorologicalInfos>
			</Body></Report>
			""");
		var series = Assert.Single(Assert.Single(doc.MeteorologicalBody.MeteorologicalInfoGroups).TimeSeriesInfos);
		var timeDefines = series.TimeDefines.ToDictionary(t => t.TimeId);
		var kind = Assert.Single(Assert.Single(series.Items).Kinds);
		Assert.Null(kind.Name);
		Assert.Equal("発表時刻", kind.DateTimeType);
		var property = Assert.Single(kind.Properties);
		var rain = Assert.Single(property.PrecipitationParts);
		Assert.Equal(1, rain.RefId);
		var quantity = Assert.Single(rain.Base!.Value.Quantities);
		Assert.Equal("Precipitation", quantity.LocalName);
		Assert.Equal("以上", quantity.Condition);
		Assert.True(quantity.TryGetIntValue(out var amount));
		Assert.Equal(50, amount);
		Assert.Equal("９日夕方", timeDefines[quantity.RefId!.Value].Name);
		var danger = Assert.Single(Assert.Single(property.SignificancyParts).Base!.Value.Significancies);
		Assert.Equal("31", danger.Code);
		Assert.Equal("９日昼過ぎ", timeDefines[danger.RefId!.Value].Name);
	}

	[Fact(DisplayName = "高潮の時間順の予想と基準到達の期間を区別して取得する")]
	public void TidalSequenceAndCriteriaPeriod()
	{
		using var doc = Parse("""
			<Report xmlns:eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/"><Body><Warning type="市町村等"><Item>
			  <Kind><Status>発表</Status><Property><Type>高潮</Type><CriteriaPeriod><Base><Local><AreaName>海岸</AreaName>
			    <CriteriaClass><Name>警戒レベル４相当</Name><Code>41</Code></CriteriaClass>
			    <Time type="基準到達時刻">2026-11-09T15:00:00+09:00</Time><Duration type="基準超過期間">PT3H</Duration>
			  </Local></Base></CriteriaPeriod><TidalLevelPart><Base><Local><AreaName>海岸</AreaName>
			    <Sequence refID="2"><TimeModifier>ピーク</TimeModifier><eb:TidalLevel type="最高潮位" unit="m">1.8</eb:TidalLevel><Time>2026-11-09T16:00:00+09:00</Time></Sequence>
			    <Sequence refID="3"><eb:TidalLevel type="潮位" unit="m">1.0</eb:TidalLevel></Sequence>
			  </Local></Base></TidalLevelPart></Property></Kind><Area><Name>市</Name></Area>
			</Item></Warning></Body></Report>
			""");
		var property = Assert.Single(Assert.Single(Assert.Single(doc.MeteorologicalBody.Warnings).Items).Kinds).Properties.Single();
		var criteria = Assert.Single(property.CriteriaPeriod!.Value.Base!.Value.Locals);
		Assert.Equal("海岸", criteria.AreaName);
		Assert.Equal("41", criteria.CriteriaClass!.Value.Code);
		Assert.Equal("基準到達時刻", criteria.TimeType);
		Assert.Equal("PT3H", criteria.Duration);
		Assert.Equal("基準超過期間", criteria.DurationType);
		var sequences = Assert.Single(property.TidalLevelPart!.Value.Base!.Value.Locals).Sequences.ToArray();
		Assert.Equal(new int?[] { 2, 3 }, sequences.Select(s => s.RefId));
		Assert.Equal("ピーク", sequences[0].TimeModifier);
		Assert.True(Assert.Single(sequences[0].Quantities).TryGetFloatValue(out var value));
		Assert.Equal(1.8f, value);
		Assert.Null(sequences[1].Time);
	}

	[Fact(DisplayName = "本文に気象警報等がない電文は空コレクションとnullで処理できる")]
	public void NoWarningBody()
	{
		using var doc = Parse("<Report><Body/></Report>");
		var body = doc.MeteorologicalBody;
		Assert.Empty(body.Warnings);
		Assert.Empty(body.Offices);
		Assert.Empty(body.TidalWarningAdditions);
		Assert.Null(body.TargetArea);
		Assert.Null(body.Comment);
	}

	[Fact(DisplayName = "風速階級を名称とコードではなく境界値と数値で取得する")]
	public void WindSpeedLevelValues()
	{
		using var xml = XmlParser.Parse("""
			<Property><Type>風</Type><WindSpeedPart>
			  <WindSpeedLevel type="風速階級" refID="1" range="0 10 15 20 25" description="強い風">2</WindSpeedLevel>
			  <WindSpeedLevel type="風速階級" refID="2" range="0 10 15 20 25" description="不明"/>
			</WindSpeedPart></Property>
			"""u8);
		var property = new MeteorologicalInfoKindProperty(xml.Root);
		var levels = property.WindSpeedPart!.Value.WindSpeedLevels.ToArray();
		Assert.Equal(2, levels.Length);
		Assert.Equal("0 10 15 20 25", levels[0].Range);
		Assert.Equal(1, levels[0].RefId);
		Assert.Equal("強い風", levels[0].Description);
		Assert.True(levels[0].TryGetFloatValue(out var level));
		Assert.Equal(2, level);
		Assert.False(levels[1].TryGetFloatValue(out _));
		Assert.Empty(levels[1].Value);
	}

	[Fact(DisplayName = "必須の種別や時刻定義を取得できない場合はパース例外を返す")]
	public void MissingRequiredFields()
	{
		using var xml = XmlParser.Parse("<Root/>"u8);
		Assert.Throws<JmaXmlParseException>(() => new MeteorologicalWarning(xml.Root).Type);
		Assert.Throws<JmaXmlParseException>(() => new MeteorologicalArea(xml.Root).Name);
		Assert.Throws<JmaXmlParseException>(() => new MeteorologicalNextKind(xml.Root).Code);
		Assert.Throws<JmaXmlParseException>(() => new MeteorologicalTimeDefine(xml.Root).TimeId);
		Assert.Throws<JmaXmlParseException>(() => new MeteorologicalTimeDefine(xml.Root).DateTime);
		Assert.Throws<JmaXmlParseException>(() => new WarningNotice(xml.Root).StartTime);
	}
}
