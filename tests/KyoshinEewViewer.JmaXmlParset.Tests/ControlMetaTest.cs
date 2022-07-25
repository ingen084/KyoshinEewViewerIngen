using System;
using StringLiteral;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

public partial class ControlMetaTest
{
	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Control><Title>緊急地震速報（地震動予報）</Title><DateTime>2011-03-11T05:48:10Z</DateTime><Status>通常</Status><EditorialOffice>気象庁本庁</EditorialOffice><PublishingOffice>気象庁</PublishingOffice></Control></Report>")]
	private static partial ReadOnlySpan<byte> NormalBody();

	[Fact]
	public void CanGetProterties()
	{
		using var doc = new JmaXmlDocument(NormalBody());
		Assert.Equal("緊急地震速報（地震動予報）", doc.Control.Title);
		Assert.Equal(new DateTimeOffset(2011, 3, 11, 5, 48, 10, TimeSpan.Zero), doc.Control.DateTime);
		Assert.Equal("通常", doc.Control.Status);
		Assert.Equal("気象庁本庁", doc.Control.EditorialOffice);
		Assert.Equal("気象庁", doc.Control.PublishingOffice);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Control></Control></Report>")]
	private static partial ReadOnlySpan<byte> EmptyBody();

	[Fact]
	public void NonNodeToException()
	{
		using var doc = new JmaXmlDocument(EmptyBody());
		Assert.Throws<JmaXmlParseException>(() => doc.Control.Title);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.DateTime);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.Status);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.EditorialOffice);
		Assert.Throws<JmaXmlParseException>(() => doc.Control.PublishingOffice);
	}
}
