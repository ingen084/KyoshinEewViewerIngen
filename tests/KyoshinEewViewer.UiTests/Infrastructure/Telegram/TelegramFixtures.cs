using System.Security;
using System.Text;
using System.Text.Json;
using DmdataSharp.WebSocketMessages.V2;

namespace KyoshinEewViewer.UiTests.Infrastructure.Telegram;

/// <summary>
/// 電文と配信サービスの応答のテストデータ
/// </summary>
internal static class TelegramFixtures
{
	private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

	/// <summary>
	/// 地震情報の電文
	/// </summary>
	internal sealed record EarthquakeTelegram(string Id, string EventId, DateTimeOffset OriginTime, string Place)
	{
		public string Type => "VXSE53";
		public string Title => "震源・震度に関する情報";
		public string Xml => EarthquakeXml(EventId, OriginTime, Place);
	}

	public static string EarthquakeXml(string eventId, DateTimeOffset originTime, string place)
	{
		var origin = originTime.ToOffset(Jst);
		var report = origin.AddMinutes(3);
		return $"""
			<?xml version="1.0" encoding="UTF-8"?>
			<Report xmlns="http://xml.kishou.go.jp/jmaxml1/" xmlns:jmx="http://xml.kishou.go.jp/jmaxml1/">
			  <Control>
			    <Title>震源・震度に関する情報</Title>
			    <DateTime>{report.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</DateTime>
			    <Status>通常</Status>
			    <EditorialOffice>気象庁本庁</EditorialOffice>
			    <PublishingOffice>気象庁</PublishingOffice>
			  </Control>
			  <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
			    <Title>震源・震度情報</Title>
			    <ReportDateTime>{report:yyyy-MM-ddTHH:mm:sszzz}</ReportDateTime>
			    <TargetDateTime>{report:yyyy-MM-ddTHH:mm:sszzz}</TargetDateTime>
			    <EventID>{eventId}</EventID>
			    <InfoType>発表</InfoType>
			    <Serial>1</Serial>
			    <InfoKind>地震情報</InfoKind>
			    <InfoKindVersion>1.0_1</InfoKindVersion>
			    <Headline>
			      <Text>{origin:dd日HH時mm分}ころ、地震がありました。</Text>
			    </Headline>
			  </Head>
			  <Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/seismology1/" xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/">
			    <Earthquake>
			      <OriginTime>{origin:yyyy-MM-ddTHH:mm:sszzz}</OriginTime>
			      <ArrivalTime>{origin:yyyy-MM-ddTHH:mm:sszzz}</ArrivalTime>
			      <Hypocenter>
			        <Area>
			          <Name>{SecurityElement.Escape(place)}</Name>
			          <Code type="震央地名">350</Code>
			          <jmx_eb:Coordinate description="北緯３５．７度　東経１４０．８度　深さ　３０ｋｍ" datum="日本測地系">+35.7+140.8-30000/</jmx_eb:Coordinate>
			        </Area>
			      </Hypocenter>
			      <jmx_eb:Magnitude type="Mj" description="Ｍ４．５">4.5</jmx_eb:Magnitude>
			    </Earthquake>
			    <Intensity>
			      <Observation>
			        <CodeDefine>
			          <Type xpath="Pref/Code">地震情報／都道府県等</Type>
			          <Type xpath="Pref/Area/Code">地震情報／細分区域</Type>
			          <Type xpath="Pref/Area/City/Code">気象・地震・火山情報／市町村等</Type>
			          <Type xpath="Pref/Area/City/IntensityStation/Code">震度観測点</Type>
			        </CodeDefine>
			        <MaxInt>3</MaxInt>
			        <Pref>
			          <Name>千葉県</Name>
			          <Code>12</Code>
			          <MaxInt>3</MaxInt>
			          <Area>
			            <Name>千葉県北東部</Name>
			            <Code>340</Code>
			            <MaxInt>3</MaxInt>
			            <City>
			              <Name>銚子市</Name>
			              <Code>1220200</Code>
			              <MaxInt>3</MaxInt>
			              <IntensityStation>
			                <Name>銚子市川口町＊</Name>
			                <Code>1220221</Code>
			                <Int>3</Int>
			              </IntensityStation>
			            </City>
			          </Area>
			        </Pref>
			      </Observation>
			    </Intensity>
			    <Comments>
			      <ForecastComment codeType="固定付加文">
			        <Text>この地震による津波の心配はありません。</Text>
			        <Code>0215</Code>
			      </ForecastComment>
			    </Comments>
			  </Body>
			</Report>
			""";
	}

	/// <summary>
	/// 緊急地震速報（地震動予報）の電文
	/// </summary>
	public static string EewXml(string eventId, int serial, DateTimeOffset originTime, string place)
	{
		var origin = originTime.ToOffset(Jst);
		var report = origin.AddSeconds(5);
		return $"""
			<?xml version="1.0" encoding="UTF-8"?>
			<Report xmlns="http://xml.kishou.go.jp/jmaxml1/" xmlns:jmx="http://xml.kishou.go.jp/jmaxml1/">
			  <Control>
			    <Title>緊急地震速報（地震動予報）</Title>
			    <DateTime>{report.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</DateTime>
			    <Status>通常</Status>
			    <EditorialOffice>気象庁本庁</EditorialOffice>
			    <PublishingOffice>気象庁</PublishingOffice>
			  </Control>
			  <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
			    <Title>緊急地震速報（地震動予報）</Title>
			    <ReportDateTime>{report:yyyy-MM-ddTHH:mm:sszzz}</ReportDateTime>
			    <TargetDateTime>{report:yyyy-MM-ddTHH:mm:sszzz}</TargetDateTime>
			    <EventID>{eventId}</EventID>
			    <InfoType>発表</InfoType>
			    <Serial>{serial}</Serial>
			    <InfoKind>緊急地震速報</InfoKind>
			    <InfoKindVersion>1.0_0</InfoKindVersion>
			    <Headline>
			      <Text />
			    </Headline>
			  </Head>
			  <Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/seismology1/" xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/">
			    <Earthquake>
			      <OriginTime>{origin:yyyy-MM-ddTHH:mm:sszzz}</OriginTime>
			      <ArrivalTime>{origin:yyyy-MM-ddTHH:mm:sszzz}</ArrivalTime>
			      <Hypocenter>
			        <Area>
			          <Name>{SecurityElement.Escape(place)}</Name>
			          <Code type="震央地名">350</Code>
			          <jmx_eb:Coordinate description="北緯３５．７度　東経１４０．８度　深さ　３０ｋｍ" datum="日本測地系">+35.7+140.8-30000/</jmx_eb:Coordinate>
			        </Area>
			        <Accuracy>
			          <Epicenter rank="4" rank2="4">4</Epicenter>
			          <Depth rank="4">4</Depth>
			          <MagnitudeCalculation rank="4">4</MagnitudeCalculation>
			          <NumberOfMagnitudeCalculation>4</NumberOfMagnitudeCalculation>
			        </Accuracy>
			      </Hypocenter>
			      <jmx_eb:Magnitude type="Mj" description="Ｍ４．５">4.5</jmx_eb:Magnitude>
			    </Earthquake>
			    <Intensity>
			      <Forecast>
			        <CodeDefine>
			          <Type xpath="Pref/Code">地震情報／都道府県等</Type>
			          <Type xpath="Pref/Area/Code">緊急地震速報／府県予報区</Type>
			        </CodeDefine>
			        <ForecastInt>
			          <From>3</From>
			          <To>3</To>
			        </ForecastInt>
			      </Forecast>
			    </Intensity>
			  </Body>
			</Report>
			""";
	}

	/// <summary>
	/// dmdata の WebSocket で配信される緊急地震速報
	/// </summary>
	public static DataWebSocketMessage DmdataEewMessage(string id, string eventId, int serial, DateTimeOffset originTime, string place)
	{
		var report = originTime.ToOffset(Jst).AddSeconds(5);
		var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(EewXml(eventId, serial, originTime, place)));
		var json = $$"""
			{
			  "type": "data",
			  "version": "2.0",
			  "classification": "eew.forecast",
			  "id": "{{id}}",
			  "passing": [{ "name": "fixture", "time": "{{report:O}}" }],
			  "head": { "type": "VXSE45", "author": "JPOS", "time": "{{report:O}}", "test": false },
			  "xmlReport": {
			    "control": { "title": "緊急地震速報（地震動予報）", "dateTime": "{{report.UtcDateTime:O}}", "status": "通常", "editorialOffice": "気象庁本庁", "publishingOffice": "気象庁" },
			    "head": { "title": "緊急地震速報（地震動予報）", "reportDateTime": "{{report:O}}", "targetDateTime": "{{report:O}}", "eventId": "{{eventId}}", "serial": "{{serial}}", "infoType": "発表", "infoKind": "緊急地震速報", "infoKindVersion": "1.0_0" }
			  },
			  "format": "xml",
			  "encoding": "base64",
			  "body": "{{body}}"
			}
			""";
		return JsonSerializer.Deserialize<DataWebSocketMessage>(json) ?? throw new InvalidOperationException("WebSocketメッセージを生成できません。");
	}

	public static string DmdataContractList(params string[] classifications)
	{
		var items = classifications.Select((c, i) =>
			$$"""{"id":{{i + 1}},"planId":{{i + 1}},"planName":"テスト","classification":"{{c}}","price":{"day":0,"month":0},"start":"2026-01-01T00:00:00+09:00","isValid":true,"connectionCounts":1}""");
		return $$"""{"responseId":"fixture","responseTime":"2026-10-18T10:00:00+09:00","status":"ok","items":[{{string.Join(",", items)}}]}""";
	}

	public static string DmdataTelegramList(IEnumerable<EarthquakeTelegram> telegrams, string dataApiBaseUrl, int nextPooling, int nextPoolingInterval)
	{
		var items = telegrams.Select((t, i) =>
		{
			var report = t.OriginTime.ToOffset(Jst).AddMinutes(3);
			return $$"""
				{"serial":{{i + 1}},"classification":"telegram.earthquake","id":"{{t.Id}}",
				 "head":{"type":"{{t.Type}}","author":"JPOS","time":"{{report:O}}","test":false},
				 "receivedTime":"{{report:O}}",
				 "xmlReport":{"control":{"title":"{{t.Title}}","dateTime":"{{report.UtcDateTime:O}}","status":"通常","editorialOffice":"気象庁本庁","publishingOffice":"気象庁"},
				              "head":{"title":"震源・震度情報","reportDateTime":"{{report:O}}","targetDateTime":"{{report:O}}","eventId":"{{t.EventId}}","serial":"1","infoType":"発表","infoKind":"地震情報","infoKindVersion":"1.0_1"} },
				 "format":"xml","url":"{{dataApiBaseUrl}}/v1/{{t.Id}}"}
				""";
		});
		return $$"""{"responseId":"fixture","responseTime":"2026-10-18T10:00:00+09:00","status":"ok","items":[{{string.Join(",", items)}}],"nextToken":"next","nextPooling":"{{nextPooling}}","nextPoolingInterval":{{nextPoolingInterval}} }""";
	}

	public static string DmdataStationList()
		=> """{"responseId":"fixture","responseTime":"2026-10-18T10:00:00+09:00","status":"ok","changeTime":"2026-01-01T00:00:00+09:00","version":"1","items":[]}""";

	/// <summary>
	/// 防災情報XMLのAtomフィード
	/// </summary>
	public static string JmaFeed(string feedUrl, IEnumerable<(string Url, string Title, DateTimeOffset Updated)> entries)
	{
		var items = entries.Select(e => $"""
			  <entry>
			    <title>{SecurityElement.Escape(e.Title)}</title>
			    <id>{e.Url}</id>
			    <updated>{e.Updated:yyyy-MM-ddTHH:mm:sszzz}</updated>
			    <author><name>気象庁</name></author>
			    <link type="application/xml" href="{e.Url}" />
			    <content type="text">テスト</content>
			  </entry>
			""");
		return $"""
			<?xml version="1.0" encoding="utf-8"?>
			<feed xmlns="http://www.w3.org/2005/Atom" lang="ja">
			  <title>高頻度（地震火山）</title>
			  <subtitle>JMAXML publishing feed</subtitle>
			  <updated>2026-10-18T10:00:00+09:00</updated>
			  <id>{feedUrl}</id>
			  <link href="{feedUrl}" rel="self" />
			  <rights>Published by Japan Meteorological Agency</rights>
			{string.Join("\n", items)}
			</feed>
			""";
	}
}
