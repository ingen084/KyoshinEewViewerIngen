using System;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

public partial class ControlMetaTest
{
	private static ReadOnlySpan<byte> NormalBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Control><Title>緊急地震速報（地震動予報）</Title><DateTime>2011-03-11T05:48:10Z</DateTime><Status>通常</Status><EditorialOffice>気象庁本庁</EditorialOffice><PublishingOffice>気象庁</PublishingOffice></Control></Report>"u8;

	[Fact]
	public void CanGetProterties()
	{
		var doc = JmaXml.Read<TestView>(NormalBody());
		Assert.Equal("緊急地震速報（地震動予報）", doc.Control.Title);
		Assert.Equal(new DateTimeOffset(2011, 3, 11, 5, 48, 10, TimeSpan.Zero), doc.Control.DateTime);
		Assert.Equal("通常", doc.Control.Status);
		Assert.Equal("気象庁本庁", doc.Control.EditorialOffice);
		Assert.Equal("気象庁", doc.Control.PublishingOffice);
	}

	private static ReadOnlySpan<byte> EmptyBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Control></Control></Report>"u8;

	[Fact]
	public void NonNodeToException()
	{
		var doc = JmaXml.Read<TestView>(EmptyBody());
		Assert.Throws<JmaXmlParseException>(() => doc.Control.Title);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.DateTime);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.Status);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.EditorialOffice);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.PublishingOffice);
	}
}
