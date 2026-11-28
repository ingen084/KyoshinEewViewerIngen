using KyoshinEewViewer.JmaXmlParser.Data;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

public class JmaXmlReadingTest
{
	private static JmaXmlDocument<TestView> Read(string xml)
		=> JmaXml.Read<TestView>(Encoding.UTF8.GetBytes(xml));

	[Fact(DisplayName = "宣言・コメント・CDATA・BOM を含む XML を、Stream からも読み取れる")]
	public void ReadsDeclarationsCommentsAndCData()
	{
		var xml = """
			﻿<?xml version="1.0" encoding="UTF-8"?>
			<!DOCTYPE Report [ <!ENTITY a "b"> ]>
			<!-- コメント <Title>無視</Title> -->
			<Report>
				<Control><Title><![CDATA[<題名> & 記号]]></Title><Status>通常</Status></Control>
			</Report>
			<!-- 末尾のコメント -->
			"""u8.ToArray();

		Assert.Equal("<題名> & 記号", JmaXml.Read<TestView>(xml).Control.Title);

		using var seekable = new System.IO.MemoryStream(xml);
		Assert.Equal("通常", JmaXml.Read<TestView>(seekable).Control.Status);
		using var nonSeekable = new System.IO.Compression.DeflateStream(Compress(xml), System.IO.Compression.CompressionMode.Decompress);
		Assert.Equal("通常", JmaXml.Read<TestView>(nonSeekable).Control.Status);

		static System.IO.MemoryStream Compress(byte[] data)
		{
			var buffer = new System.IO.MemoryStream();
			using (var deflate = new System.IO.Compression.DeflateStream(buffer, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
				deflate.Write(data);
			buffer.Position = 0;
			return buffer;
		}
	}

	[Fact(DisplayName = "読み取り対象でない要素は中身の構造にかかわらず読み飛ばされ、後続の要素を読める")]
	public void UnreferencedElementsAreSkipped()
	{
		string[] unknownElements =
		[
			"<Unknown/>",
			"<Unknown a=\"1\" />",
			"<Unknown></Unknown>",
			"<Unknown a=\"x > y\" b='/>'><Control><Title>無視</Title></Control></Unknown>",
			"<Unknown><Unknown><Unknown/></Unknown><Unknown a='1'/></Unknown>",
			"<Unknown><!-- </Unknown> <Title> --><![CDATA[</Unknown><Title>]]><?pi </Unknown> ?></Unknown>",
			"<Unknown>" + new string('あ', 100) + "<Child attribute=\"" + new string('>', 100) + "\">" + new string(' ', 100) + "</Child\n></Unknown >",
		];
		foreach (var unknown in unknownElements)
		{
			for (var padding = 0; padding < 70; padding++)
			{
				var report = Read($"<Report>{new string(' ', padding)}{unknown}<Control>{unknown}<Title>題名</Title>{unknown}<Status>通常</Status></Control>{unknown}<Head>{unknown}<Title>見出し</Title></Head></Report>");

				Assert.Equal("題名", report.Control.Title);
				Assert.Equal("通常", report.Control.Status);
				Assert.Equal("見出し", report.Head.Title);
			}
		}
	}

	[Fact(DisplayName = "区切り文字が64バイトのブロック境界のどこにあっても同じ結果になる")]
	public void DelimitersAcrossBlockBoundaries()
	{
		for (var padding = 0; padding < 140; padding++)
		{
			var space = new string(' ', padding);
			var report = Read($"<Report {space}xmlns=\"x\"{space}>{space}<Body{space}><Earthquake><Hypocenter><Area><Name>{space}震央{padding}{space}</Name{space}><jmx_eb:Coordinate {space}type=\"種別{padding}\"{space}>+35.0+135.0/</jmx_eb:Coordinate><Code/></Area></Hypocenter></Earthquake></Body{space}>{space}</Report{space}>{space}");

			var area = report.EarthquakeBody.Earthquake!.Hypocenter.Area;
			Assert.Equal($"震央{padding}", area.Name);
			Assert.Equal("", area.Code);
			var coordinate = Assert.Single(area.Coordinates);
			Assert.Equal($"種別{padding}", coordinate.Type);
			Assert.Equal("+35.0+135.0/", coordinate.Value);
		}
	}

	[Fact(DisplayName = "不正な XML は種類に応じた例外になる")]
	public void InvalidXmlThrows()
	{
		Assert.Throws<JmaXmlParseException>(() => Read("<Other><Control/></Other>"));

		string[] invalidXmls =
		[
			"",
			"<Report><Control></Head></Report>",
			"<Report><Control>",
			"<Report><Control id></Control></Report>",
			"<Report><Control id=\"x></Control></Report>",
			"<Report><!-- 閉じていない </Report>",
			"</Report>",
			"<Report><Unknown><a></Report>",
			"<Report><Unknown><a b></a></Unknown></Report>",
			"<Report><Unknown><!-- 閉じていない </Unknown></Report>",
			"<Report><Unknown></a",
		];
		foreach (var xml in invalidXmls)
			Assert.Throws<FormatException>(() => Read(xml));
	}

	[Fact(DisplayName = "必須の要素や属性の欠落は、読み取り時ではなくメンバーを参照した時点で例外になる")]
	public void MissingRequiredThrowsOnAccess()
	{
		var report = Read("<Report><Body><Earthquake><Hypocenter><Area><jmx_eb:Coordinate>+35+135/</jmx_eb:Coordinate></Area></Hypocenter></Earthquake></Body></Report>");

		Assert.Throws<JmaXmlParseException>(() => report.Control);
		var earthquake = report.EarthquakeBody.Earthquake!;
		Assert.Null(earthquake.OriginTime);
		Assert.Throws<JmaXmlParseException>(() => earthquake.Magnitude);
		Assert.Throws<JmaXmlParseException>(() => earthquake.Hypocenter.Area.Name);
		Assert.Null(Assert.Single(earthquake.Hypocenter.Area.Coordinates).Type);
		Assert.Throws<JmaXmlParseException>(() => earthquake.Hypocenter.Accuracy);
		Assert.Null(report.EarthquakeBody.Comments);
		Assert.Null(report.EarthquakeBody.Intensity);
	}

	[Fact(DisplayName = "子要素の下の要素は、途中の要素が任意か必須かに応じて null か例外になる")]
	public void NestedPathsFollowContainerRequirement()
	{
		var withoutContainers = Read("<Report><Body><Comments/><Intensity><Forecast><Pref><Area><Name>地域</Name></Area></Pref></Forecast></Intensity></Body></Report>");
		Assert.Null(withoutContainers.EarthquakeBody.Comments!.ForecastCommentText);
		var area = withoutContainers.EarthquakeBody.Intensity!.Forecast!.Prefs[0].Areas[0];
		Assert.Throws<JmaXmlParseException>(() => area.ForecastIntFrom);
		Assert.Null(area.ForecastLgIntFrom);

		var emptyContainers = Read("<Report><Body><Comments><ForecastComment/></Comments><Intensity><Forecast><Pref><Area><ForecastLgInt/><ForecastInt><From>4</From><To>over</To></ForecastInt></Area></Pref></Forecast></Intensity></Body></Report>");
		Assert.Throws<JmaXmlParseException>(() => emptyContainers.EarthquakeBody.Comments!.ForecastCommentText);
		area = emptyContainers.EarthquakeBody.Intensity!.Forecast!.Prefs[0].Areas[0];
		Assert.Equal("4", area.ForecastIntFrom);
		Assert.Equal("over", area.ForecastIntTo);
		Assert.Throws<JmaXmlParseException>(() => area.ForecastLgIntFrom);
	}

	[Fact(DisplayName = "同じ名前の要素が複数ある場合、単一の値は最初の要素から読み、一覧はすべての要素を読む")]
	public void FirstElementWinsForSingleValues()
	{
		var report = Read("<Report><Control><Title>一つ目</Title><Title>二つ目</Title></Control><Control><Title>別の管理部</Title></Control>"
			+ "<Body><Earthquake><Hypocenter><Area><jmx_eb:Coordinate type=\"a\">1</jmx_eb:Coordinate><jmx_eb:Coordinate type=\"b\">2</jmx_eb:Coordinate></Area></Hypocenter></Earthquake></Body></Report>");

		Assert.Equal("一つ目", report.Control.Title);
		var area = report.EarthquakeBody.Earthquake!.Hypocenter.Area;
		Assert.Equal("a", area.Coordinate.Type);
		Assert.Equal(["1", "2"], area.Coordinates.Select(c => c.Value));
	}

	[Fact(DisplayName = "xsi:nil が true の日時は null になり、値がなければ欠落として例外になる")]
	public void NilDateTime()
	{
		static JmaXmlDocument<TestView> Head(string content) => Read($"<Report><Head>{content}</Head></Report>");

		Assert.Null(Head("<TargetDateTime xsi:nil=\"true\"/>").Head.TargetDateTime);
		Assert.Null(Head("<TargetDateTime xsi:nil=\"true\"></TargetDateTime>").Head.TargetDateTime);
		Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(9)), Head("<TargetDateTime xsi:nil=\"false\">2024-01-01T00:00:00+09:00</TargetDateTime>").Head.TargetDateTime);
		Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(9)), Head("<TargetDateTime>2024-01-01T00:00:00+09:00</TargetDateTime>").Head.TargetDateTime);
		Assert.Throws<JmaXmlParseException>(() => Head("<TargetDateTime/>").Head.TargetDateTime);
		Assert.Throws<JmaXmlParseException>(() => Head("<Title>題名</Title>").Head.TargetDateTime);
	}

	[Fact(DisplayName = "同じ Body 要素を、地震と津波の両方の内容部として読める")]
	public void ReadsBodyAsMultipleTypes()
	{
		var report = Read("<Report><Body><Earthquake><Hypocenter><Area><Name>震央A</Name></Area></Hypocenter></Earthquake><Earthquake><Hypocenter><Area><Name>震央B</Name></Area></Hypocenter></Earthquake><Text>本文</Text></Body></Report>");

		Assert.Equal("震央A", report.EarthquakeBody.Earthquake!.Hypocenter.Area.Name);
		Assert.Equal(["震央A", "震央B"], report.TsunamiBody.Earthquakes.Select(e => e.Hypocenter.Area.Name));
		Assert.Equal("本文", report.TsunamiBody.Text);
	}

	[Fact(DisplayName = "台風情報の多段の経路を読める")]
	public void ReadsTyphoonPaths()
	{
		var report = Read("""
			<Report><Body><MeteorologicalInfos type="台風情報">
				<MeteorologicalInfo>
					<DateTime type="実況">2024-08-01T09:00:00+09:00</DateTime>
					<Item>
						<Kind><Property><Type>呼称</Type><TyphoonNamePart><Number>2405</Number></TyphoonNamePart></Property></Kind>
						<Kind><Property><Type>中心</Type><CenterPart><jmx_eb:Pressure type="中心気圧" unit="hPa">980</jmx_eb:Pressure></CenterPart></Property><Property><Type>読まない</Type></Property></Kind>
						<Area><jmx_eb:Circle type="予報円"><jmx_eb:Axes><jmx_eb:Axis><jmx_eb:Direction type="方向" unit="８方位漢字">北</jmx_eb:Direction><jmx_eb:Radius type="半径" unit="km">120</jmx_eb:Radius></jmx_eb:Axis></jmx_eb:Axes></jmx_eb:Circle><jmx_eb:Circle type="暴風警戒域"/></Area>
					</Item>
				</MeteorologicalInfo>
				<MeteorologicalInfo><DateTime>2024-08-02T09:00:00+09:00</DateTime><Item><Kind/></Item></MeteorologicalInfo>
				<MeteorologicalInfo/>
			</MeteorologicalInfos></Body></Report>
			""");

		Assert.Equal("台風情報", report.MeteorologicalBody.Type);
		var infos = report.MeteorologicalBody.MeteorologicalInfos;
		Assert.Equal(3, infos.Count);

		Assert.Equal("実況", infos[0].DateTimeType);
		Assert.Equal(new DateTimeOffset(2024, 8, 1, 9, 0, 0, TimeSpan.FromHours(9)), infos[0].DateTime);
		Assert.Equal(["呼称", "中心"], infos[0].MeteorologicalInfoKindProperties.Select(p => p.Type));
		Assert.Equal("2405", infos[0].MeteorologicalInfoKindProperties[0].TyphoonNamePart.Number);
		Assert.True(infos[0].MeteorologicalInfoKindProperties[1].CenterPart.Pressure.TryGetIntValue(out var pressure));
		Assert.Equal(980, pressure);
		Assert.Equal(["予報円", "暴風警戒域"], infos[0].TyphoonCircles.Select(c => c.Type));
		var axis = Assert.Single(infos[0].TyphoonCircles[0].Axes);
		Assert.Equal("北", axis.Direction.Value);
		Assert.Equal("120", Assert.Single(axis.Radiuses).Value);
		Assert.Throws<JmaXmlParseException>(() => infos[0].TyphoonCircles[1].Axes);

		// Property のない Kind がある場合、一覧を参照した時点で例外になる
		Assert.Throws<JmaXmlParseException>(() => infos[1].DateTimeType);
		Assert.Throws<JmaXmlParseException>(() => infos[1].MeteorologicalInfoKindProperties);
		Assert.Throws<JmaXmlParseException>(() => infos[1].TyphoonCircles);

		Assert.Throws<JmaXmlParseException>(() => infos[2].DateTime);
		Assert.Empty(infos[2].MeteorologicalInfoKindProperties);
	}

	[Fact(DisplayName = "プロパティパターンだけで参照したメンバーも読み取られる")]
	public void PropertyPatternsSelectMembers()
	{
		var report = JmaXml.Read<PatternOnlyView>("<Report><Control><Title>題名</Title><EditorialOffice>気象庁本庁</EditorialOffice></Control><Head><Serial>3</Serial><Headline><Text>見出し文</Text></Headline></Head></Report>"u8);

		if (report is not { Control: { Title: "題名", EditorialOffice: var office }, Head.Headline.Text: var text, Head.Serial: "3" })
		{
			Assert.Fail("パターンに一致しませんでした");
			return;
		}
		Assert.Equal("気象庁本庁", office);
		Assert.Equal("見出し文", text);
	}

	[Fact(DisplayName = "読み取り対象に含まれないメンバーを参照すると、値なしではなく例外になる")]
	public void UnselectedMemberThrows()
	{
		var report = JmaXml.Read<TitleOnlyView>("<Report><Control><Title>題名</Title><Status>通常</Status></Control></Report>"u8);
		Assert.Equal("題名", report.Control.Title);

		// ソースジェネレーターに参照として拾われないよう、リフレクション経由で読む
		var status = typeof(ControlMeta<TitleOnlyView>).GetProperty("Status")!;
		var exception = Assert.Throws<TargetInvocationException>(() => status.GetValue(report.Control));
		Assert.IsType<InvalidOperationException>(exception.InnerException);
		var head = typeof(JmaXmlDocument<TitleOnlyView>).GetProperty("Head")!;
		exception = Assert.Throws<TargetInvocationException>(() => head.GetValue(report));
		Assert.IsType<InvalidOperationException>(exception.InnerException);
	}
}
