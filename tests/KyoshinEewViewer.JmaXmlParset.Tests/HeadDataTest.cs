using KyoshinEewViewer.JmaXmlParser;
using System;
using StringLiteral;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParset.Tests;

public partial class HeadDataTest
{
	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Head><Title>奈良県気象警報・注意報</Title><ReportDateTime>2011-09-03T12:00:00+09:00</ReportDateTime><TargetDateTime>2011-09-03T12:00:00+09:00</TargetDateTime><EventID /><InfoType>発表</InfoType><Serial /><InfoKind>気象警報・注意報</InfoKind><InfoKindVersion>1.0_0</InfoKindVersion></Head></Report>")]
	private static partial ReadOnlySpan<byte> NormalBody1();

	[Fact]
	public void CanGetProterties()
	{
		using var doc = new JmaXmlDocument(NormalBody1());
		Assert.Equal("奈良県気象警報・注意報", doc.Head.Title);
		Assert.Equal(new DateTimeOffset(2011, 9, 3, 12, 0, 0, TimeSpan.FromHours(9)), doc.Head.ReportDateTime);
		Assert.Equal(new DateTimeOffset(2011, 9, 3, 12, 0, 0, TimeSpan.FromHours(9)), doc.Head.TargetDateTime);
		Assert.Equal("", doc.Head.EventId);
		Assert.Equal("発表", doc.Head.InfoType);
		Assert.Equal("", doc.Head.Serial);
		Assert.Equal("気象警報・注意報", doc.Head.InfoKind);
		Assert.Equal("1.0_0", doc.Head.InfoKindVersion);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Head><Headline><Text>あああ</Text></Headline></Head></Report>")]
	private static partial ReadOnlySpan<byte> NormalBody2();

	[Fact]
	public void CanGetInformationEmptyHeadline()
	{
		using var doc = new JmaXmlDocument(NormalBody2());
		Assert.Equal("あああ", doc.Head.Headline.Text);
		Assert.Empty(doc.Head.Headline.Informations);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Head><Headline><Information type=\"type1\"><Item><Kind><Name>大雨警報</Name><Code>03</Code><Condition>土砂災害、浸水害</Condition></Kind></Item></Information></Headline></Head></Report>")]
	private static partial ReadOnlySpan<byte> NormalBody3();

	[Fact]
	public void CanGetHeadlineInformationWithKind()
	{
		using var doc = new JmaXmlDocument(NormalBody3());
		Assert.Collection(doc.Head.Headline.Informations, info =>
		{
			Assert.Equal("type1", info.Type);
			Assert.Collection(info.Items, item =>
			{
				Assert.Collection(item.Kinds, kind =>
				{
					Assert.Equal("大雨警報", kind.Name);
					Assert.Equal("03", kind.Code);
					Assert.Equal("土砂災害、浸水害", kind.Condition);
				});
			});
		});
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Head><Headline><Information type=\"type1\"><Item><Areas codeType=\"気象情報／府県予報区・細分区域等\"><Area><Name>奈良県</Name><Code>290000</Code></Area></Areas></Item></Information></Headline></Head></Report>")]
	private static partial ReadOnlySpan<byte> NormalBody4();

	[Fact]
	public void CanGetHeadlineInformationWithArea()
	{
		using var doc = new JmaXmlDocument(NormalBody4());
		Assert.Collection(doc.Head.Headline.Informations, info =>
		{
			Assert.Equal("type1", info.Type);
			Assert.Collection(info.Items, item =>
			{
				Assert.Equal("気象情報／府県予報区・細分区域等", item.AreaCodeType);
				Assert.Collection(item.Areas, kind =>
				{
					Assert.Equal("奈良県", kind.Name);
					Assert.Equal("290000", kind.Code);
				});
			});
		});
	}
}
