using KyoshinEewViewer.JmaXmlParser.Data;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

// 気象庁の2026-09-17版公式サンプル。XMLの末尾空行のみ整理。
// 大きな集約通報・時系列サンプルは原文のままgzip圧縮して収録する。
// https://xml.kishou.go.jp/jmaxml_20260917_Samples.zip
public class WeatherWarningSampleTests
{
	private static readonly XNamespace Mete = "http://xml.kishou.go.jp/jmaxml1/body/meteorology1/";
	private static readonly XNamespace Eb = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/";

	public static IEnumerable<object[]> Samples => new[]
	{
		"15_06_02_100514_VPWW50.xml", "15_12_03_161130_VPWW53.xml", "15_11_02_150916_VPWW54.xml.gz",
		"15_16_01_241031_VPWW56.xml", "15_16_02_251222_VPWW57.xml", "15_16_03_241226_VPWW55.xml",
		"15_16_04_251222_VPWW58.xml", "15_16_05_241226_VPWW59.xml", "15_16_06_241226_VPWW60.xml",
		"15_16_07_250825_VPWW61.xml", "15_16_08_250630_VPWW57.xml",
		"81_01_01_260129_VPWP50.xml.gz", "15_18_01_250630_VPWS50.xml.gz",
	}.Select(name => new object[] { name });

	private static byte[] ReadSample(string name)
	{
		using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WeatherWarnings", name));
		using var result = new MemoryStream();
		if (name.EndsWith(".gz", StringComparison.Ordinal))
		{
			using var gzip = new GZipStream(input, CompressionMode.Decompress);
			gzip.CopyTo(result);
		}
		else
			input.CopyTo(result);
		return result.ToArray();
	}

	[Theory(DisplayName = "公式の新旧気象警報・注意報と集約通報および時系列情報の値と階層を取得できる")]
	[MemberData(nameof(Samples))]
	public void OfficialWarningValuesMatchXml(string name)
	{
		var bytes = ReadSample(name);
		using var doc = new JmaXmlDocument(bytes);
		using var stream = new MemoryStream(bytes);
		var expected = XDocument.Load(stream).Root!.Element(Mete + "Body")!;
		var body = doc.MeteorologicalBody;
		AssertSequence(expected.Elements(Mete + "Warning"), body.Warnings, (xml, warning) =>
		{
			Assert.Equal((string?)xml.Attribute("type"), warning.Type);
			AssertSequence(xml.Elements(Mete + "Item"), warning.Items, AssertItem);
		});
		Assert.Equal(expected.Elements(Mete + "Notice").Select(e => e.Value), body.Notices);
		AssertSequence(expected.Elements(Mete + "MeteorologicalInfos"), body.MeteorologicalInfoGroups, (xml, group) =>
		{
			Assert.Equal((string?)xml.Attribute("type"), group.Type);
			AssertSequence(xml.Elements(Mete + "TimeSeriesInfo"), group.TimeSeriesInfos, (seriesXml, series) =>
			{
				AssertSequence(seriesXml.Element(Mete + "TimeDefines")!.Elements(Mete + "TimeDefine"), series.TimeDefines, (timeXml, time) =>
				{
					Assert.Equal(int.Parse(timeXml.Attribute("timeId")!.Value), time.TimeId);
					Assert.Equal(DateTimeOffset.Parse(Text(timeXml, "DateTime")!), time.DateTime);
					Assert.Equal(Text(timeXml, "Duration"), time.Duration);
					Assert.Equal(Text(timeXml, "Name"), time.Name);
				});
				AssertSequence(seriesXml.Elements(Mete + "Item"), series.Items, AssertItem);
			});
		});
		AssertOptional(expected.Element(Mete + "Comment"), body.Comment, (xml, comment) =>
		{
			Assert.Equal(Text(xml, "Code"), comment.Code);
			AssertSequence(xml.Elements(Mete + "Text"), comment.Texts, (textXml, text) =>
			{
				Assert.Equal((string?)textXml.Attribute("type"), text.Type);
				Assert.Equal(Normalize(textXml.Value), Normalize(text.Value));
			});
		});
		AssertSequence(expected.Element(Mete + "OfficeInfo")?.Elements(Mete + "Office") ?? [], body.Offices, (xml, office) =>
		{
			Assert.Equal(Text(xml, "Name"), office.Name);
			Assert.Equal((string?)xml.Attribute("type"), office.Type);
			Assert.Equal(Text(xml, "Code"), office.Code);
			Assert.Equal(Text(xml, "ContactInfo"), office.ContactInfo);
			Assert.Equal(Text(xml, "URI"), office.Uri);
		});
		AssertSequence(expected.Element(Mete + "AdditionalInfo")?.Elements(Mete + "TidalWarningAddition") ?? [], body.TidalWarningAdditions, (xml, addition) =>
		{
			var areaXml = xml.Element(Mete + "TidalAreaPart")!;
			AssertArea(areaXml.Element(Mete + "Area")!, addition.TidalAreaPart.Area);
			Assert.Equal(areaXml.Elements(Mete + "ChargeSection").Select(e => e.Value), addition.TidalAreaPart.ChargeSections);
			AssertSequence(xml.Elements(Mete + "HydrometricStationPart"), addition.HydrometricStationParts, (stationXml, station) =>
			{
				AssertArea(stationXml.Element(Mete + "Area")!, station.Area);
				Assert.Equal(stationXml.Elements(Mete + "ChargeSection").Select(e => e.Value), station.ChargeSections);
				Assert.Equal(Text(stationXml, "Remark"), station.Remark);
				AssertSequence(stationXml.Elements(Mete + "Criteria"), station.Criteria, AssertPart);
			});
		});
	}

	private static void AssertItem(XElement xml, MeteorologicalItem item)
	{
		AssertOptional(xml.Element(Mete + "Area"), item.Area, AssertArea);
		Assert.Equal(Text(xml, "ChangeStatus"), item.ChangeStatus);
		Assert.Equal(Text(xml, "FullStatus"), item.FullStatus);
		Assert.Equal(Text(xml, "EditingMark"), item.EditingMark);
		AssertSequence(xml.Elements(Mete + "Kind"), item.Kinds, (kindXml, kind) =>
		{
			Assert.Equal(Text(kindXml, "Name"), kind.Name);
			Assert.Equal(Text(kindXml, "Code"), kind.Code);
			Assert.Equal(Text(kindXml, "Status"), kind.Status);
			Assert.Equal(Text(kindXml, "Condition"), kind.Condition);
			Assert.Equal(Text(kindXml, "ClassName"), kind.ClassName);
			Assert.Equal(ParseDate(Text(kindXml, "DateTime")), kind.DateTime);
			Assert.Equal((string?)kindXml.Element(Mete + "DateTime")?.Attribute("type"), kind.DateTimeType);
			Assert.Equal(Notes(kindXml, "Attention"), kind.AttentionNotes);
			Assert.Equal(Notes(kindXml, "Addition"), kind.AdditionNotes);
			AssertOptional(kindXml.Element(Mete + "LastKind"), kind.LastKind, AssertNextKind);
			AssertSequence(kindXml.Element(Mete + "NextKinds")?.Elements(Mete + "NextKind") ?? [], kind.NextKinds, AssertNextKind);
			AssertOptional(kindXml.Element(Mete + "WarningNotice"), kind.WarningNotice, (noticeXml, notice) =>
			{
				AssertTerm(noticeXml.Element(Mete + "StartTime")!, notice.StartTime);
				Assert.Equal(Text(noticeXml, "Note"), notice.Note);
			});
			AssertSequence(kindXml.Elements(Mete + "Property"), kind.Properties, (propertyXml, property) =>
			{
				Assert.Equal(Text(propertyXml, "Type"), property.Type);
				AssertOptional(propertyXml.Element(Mete + "WarningPeriod"), property.WarningPeriod, AssertPeriod);
				AssertOptional(propertyXml.Element(Mete + "AdvisoryPeriod"), property.AdvisoryPeriod, AssertPeriod);
				AssertOptional(propertyXml.Element(Mete + "PeakTime"), property.PeakTime, AssertTerm);
				// 未対応の部品がサンプルに含まれた場合も件数照合で検出する。
				var parts = propertyXml.Elements().Where(e => e.Name.LocalName is not ("Type" or "WarningPeriod" or "AdvisoryPeriod" or "PeakTime"));
				AssertSequence(parts, property.ForecastParts, AssertPart);
			});
		});
	}

	private static void AssertArea(XElement xml, MeteorologicalArea area)
	{
		Assert.Equal(Text(xml, "Name"), area.Name);
		Assert.Equal(Text(xml, "Code"), area.Code);
		Assert.Equal((string?)xml.Attribute("codeType"), area.CodeType);
		Assert.Equal(Text(xml, "SubCityList"), area.SubCityList);
		Assert.Equal(Text(xml, "SubCityCodeList"), area.SubCityCodeList);
		Assert.Equal(Text(xml, "Location"), area.Location);
	}

	private static void AssertNextKind(XElement xml, MeteorologicalNextKind kind)
	{
		Assert.Equal(Text(xml, "Name"), kind.Name);
		Assert.Equal(Text(xml, "Code"), kind.Code);
		Assert.Equal(Text(xml, "Condition"), kind.Condition);
		Assert.Equal(Text(xml, "Sentence"), kind.Sentence);
		Assert.Equal(ParseDate(Text(xml, "DateTime")), kind.DateTime);
		Assert.Equal((string?)xml.Element(Mete + "DateTime")?.Attribute("type"), kind.DateTimeType);
	}

	private static void AssertPeriod(XElement xml, MeteorologicalPeriod period)
	{
		AssertOptional(xml.Element(Mete + "StartTime"), period.StartTime, AssertTerm);
		AssertOptional(xml.Element(Mete + "EndTime"), period.EndTime, AssertTerm);
		AssertOptional(xml.Element(Mete + "ZoneTime"), period.ZoneTime, AssertTerm);
		Assert.Equal(Text(xml, "OverTime"), period.OverTime);
	}

	private static void AssertTerm(XElement xml, ForecastTerm term)
	{
		Assert.Equal(Text(xml, "Date"), term.Date);
		Assert.Equal(Text(xml, "Term"), term.Term);
	}

	private static void AssertPart(XElement xml, MeteorologicalForecastPart part)
	{
		Assert.Equal(xml.Name.LocalName, part.Name);
		Assert.Equal(ParseInt((string?)xml.Attribute("refID")), part.RefId);
		Assert.Equal(Text(xml, "AreaName"), part.AreaName);
		Assert.Equal(Normalize(Text(xml, "Sentence")), Normalize(part.Sentence));
		Assert.Equal((string?)xml.Element(Mete + "Sentence")?.Attribute("type"), part.SentenceType);
		Assert.Equal(Text(xml, "TimeModifier"), part.TimeModifier);
		Assert.Equal(ParseDate(Text(xml, "Time")), part.Time);
		Assert.Equal((string?)xml.Element(Mete + "Time")?.Attribute("type"), part.TimeType);
		Assert.Equal(Text(xml, "Duration"), part.Duration);
		Assert.Equal((string?)xml.Element(Mete + "Duration")?.Attribute("type"), part.DurationType);
		Assert.Equal(Text(xml, "Remark"), part.Remark);
		Assert.Equal(Notes(xml, "Attention"), part.AttentionNotes);
		Assert.Equal(Notes(xml, "Addition"), part.AdditionNotes);
		AssertOptional(xml.Element(Mete + "PeakTime"), part.PeakTime, AssertTerm);
		AssertOptional(xml.Element(Mete + "Base"), part.Base, AssertPart);
		AssertSequence(xml.Elements(Mete + "Temporary"), part.Temporaries, AssertPart);
		AssertSequence(xml.Elements(Mete + "Becoming"), part.Becomings, AssertPart);
		AssertSequence(xml.Elements(Mete + "SubArea"), part.SubAreas, AssertPart);
		AssertSequence(xml.Elements(Mete + "Local"), part.Locals, AssertPart);
		AssertSequence(xml.Elements(Mete + "Sequence"), part.Sequences, AssertPart);
		AssertOptional(xml.Element(Mete + "CriteriaClass"), part.CriteriaClass, AssertClass);
		AssertSequence(xml.Elements(Mete + "Significancy"), part.Significancies, AssertClass);
		AssertSequence(xml.Elements(Mete + "WindSpeedLevel"), part.WindSpeedLevels, (levelXml, level) =>
		{
			Assert.Equal((string?)levelXml.Attribute("type"), level.Type);
			Assert.Equal(int.Parse(levelXml.Attribute("refID")!.Value), level.RefId);
			Assert.Equal((string?)levelXml.Attribute("range"), level.Range);
			Assert.Equal((string?)levelXml.Attribute("description"), level.Description);
			Assert.Equal(levelXml.Value.Trim(), level.Value.Trim());
		});
		AssertSequence(xml.Elements().Where(e => e.Name.Namespace == Eb), part.Quantities, (quantityXml, quantity) =>
		{
			Assert.Equal(quantityXml.Name.LocalName, quantity.LocalName);
			Assert.Equal((string?)quantityXml.Attribute("type"), quantity.Type);
			Assert.Equal((string?)quantityXml.Attribute("unit"), quantity.Unit);
			Assert.Equal((string?)quantityXml.Attribute("condition"), quantity.Condition);
			Assert.Equal((string?)quantityXml.Attribute("description"), quantity.Description);
			Assert.Equal(ParseInt((string?)quantityXml.Attribute("refID")), quantity.RefId);
			// xs:float などの数値では前後の空白に意味がない。
			Assert.Equal(Normalize(quantityXml.Value)?.Trim(), Normalize(quantity.Value)?.Trim());
		});
	}

	private static void AssertClass(XElement xml, MeteorologicalClass value)
	{
		Assert.Equal((string?)xml.Attribute("type"), value.Type);
		Assert.Equal(ParseInt((string?)xml.Attribute("refID")), value.RefId);
		Assert.Equal(Text(xml, "Name"), value.Name);
		Assert.Equal(Text(xml, "Code"), value.Code);
		Assert.Equal(Text(xml, "Condition"), value.Condition);
		Assert.Equal(Text(xml, "Remark"), value.Remark);
		Assert.Equal(Text(xml, "Sentence"), value.Sentence);
		Assert.Equal((string?)xml.Element(Mete + "Sentence")?.Attribute("type"), value.SentenceType);
	}

	private static string? Text(XElement xml, string name) => xml.Element(Mete + name)?.Value;
	private static string? Normalize(string? text) => text?.Replace("\r\n", "\n");
	private static int? ParseInt(string? text) => text == null ? null : int.Parse(text);
	private static DateTimeOffset? ParseDate(string? text) => text == null ? null : DateTimeOffset.Parse(text);
	private static IEnumerable<string> Notes(XElement xml, string name) => xml.Element(Mete + name)?.Elements(Mete + "Note").Select(e => e.Value) ?? [];

	private static void AssertOptional<T>(XElement? expected, T? actual, Action<XElement, T> verify) where T : struct
	{
		Assert.Equal(expected != null, actual.HasValue);
		if (expected != null)
			verify(expected, actual!.Value);
	}

	private static void AssertSequence<T>(IEnumerable<XElement> expected, IEnumerable<T> actual, Action<XElement, T> verify)
	{
		var xml = expected.ToArray();
		var values = actual.ToArray();
		Assert.Equal(xml.Length, values.Length);
		for (var i = 0; i < xml.Length; i++)
			verify(xml[i], values[i]);
	}
}
