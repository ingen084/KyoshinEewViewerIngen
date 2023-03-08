using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using StringLiteral;
using System;
using U8Xml;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests.Earthquake;

public partial class PrefAreaTest
{
	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Pref><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><Area></Area></Pref>")]
	private static partial ReadOnlySpan<byte> PrefBody();

	[Fact]
	public void CanGetPrefProterties()
	{
		using var doc = XmlParser.Parse(PrefBody());
		var node = new Pref(doc.Root);
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.Areas);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Area><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><City></City><IntensityStation></IntensityStation></Area>")]
	private static partial ReadOnlySpan<byte> AreaBody();

	[Fact]
	public void CanGetAreaProterties()
	{
		using var doc = XmlParser.Parse(AreaBody());
		var node = new Area(doc.Root);
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.Cities);
		Assert.Single(node.IntensityStations);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><City><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><Condition>condition</Condition><IntensityStation></IntensityStation></City>")]
	private static partial ReadOnlySpan<byte> CityBody();

	[Fact]
	public void CanGetCityProterties()
	{
		using var doc = XmlParser.Parse(CityBody());
		var node = new City(doc.Root);
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("condition", node.Condition);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.IntensityStations);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><IntensityStation><Name>name</Name><Code>code</Code><Int>int</Int><Revise>rev</Revise><Sva></Sva><LgIntPerPeriod></LgIntPerPeriod><SvaPerPeriod></SvaPerPeriod></IntensityStation>")]
	private static partial ReadOnlySpan<byte> IntensityStationBody();

	[Fact]
	public void CanGetIntensityStationProterties()
	{
		using var doc = XmlParser.Parse(IntensityStationBody());
		var node = new IntensityStation(doc.Root);
		Assert.Equal("name", node.Name);
		Assert.Equal("code", node.Code);
		Assert.Equal("int", node.Int);
		Assert.Equal("rev", node.Revise);
		Assert.NotNull(node.Sva);
		Assert.Single(node.LgIntPerPeriods);
		Assert.Single(node.SvaPerPeriods);
	}
}
