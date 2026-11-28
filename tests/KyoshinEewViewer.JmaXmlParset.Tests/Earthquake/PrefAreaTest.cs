using System;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests.Earthquake;

public partial class PrefAreaTest
{
	private static ReadOnlySpan<byte> PrefBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Intensity><Observation><Pref><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><Area></Area></Pref></Observation></Intensity></Body></Report>"u8;

	[Fact]
	public void CanGetPrefProterties()
	{
		var node = JmaXml.Read<TestView>(PrefBody()).EarthquakeBody.Intensity!.Observation!.Prefs[0];
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.Areas);
	}

	private static ReadOnlySpan<byte> AreaBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Intensity><Observation><Pref><Area><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><City></City><IntensityStation></IntensityStation></Area></Pref></Observation></Intensity></Body></Report>"u8;

	[Fact]
	public void CanGetAreaProterties()
	{
		var node = JmaXml.Read<TestView>(AreaBody()).EarthquakeBody.Intensity!.Observation!.Prefs[0].Areas[0];
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.Cities);
		Assert.Single(node.IntensityStations);
	}

	private static ReadOnlySpan<byte> CityBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Intensity><Observation><Pref><Area><City><Name>name</Name><Code>123</Code><MaxInt>maxint</MaxInt><MaxLgInt>maxLgInt</MaxLgInt><Revise>rev</Revise><Condition>condition</Condition><IntensityStation></IntensityStation></City></Area></Pref></Observation></Intensity></Body></Report>"u8;

	[Fact]
	public void CanGetCityProterties()
	{
		var node = JmaXml.Read<TestView>(CityBody()).EarthquakeBody.Intensity!.Observation!.Prefs[0].Areas[0].Cities[0];
		Assert.Equal("name", node.Name);
		Assert.Equal(123, node.Code);
		Assert.Equal("condition", node.Condition);
		Assert.Equal("maxint", node.MaxInt);
		Assert.Equal("maxLgInt", node.MaxLgInt);
		Assert.Equal("rev", node.Revise);
		Assert.Single(node.IntensityStations);
	}

	private static ReadOnlySpan<byte> IntensityStationBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Intensity><Observation><Pref><Area><City><IntensityStation><Name>name</Name><Code>code</Code><Int>int</Int><Revise>rev</Revise><Sva></Sva><LgIntPerPeriod></LgIntPerPeriod><SvaPerPeriod></SvaPerPeriod></IntensityStation></City></Area></Pref></Observation></Intensity></Body></Report>"u8;

	[Fact]
	public void CanGetIntensityStationProterties()
	{
		var node = JmaXml.Read<TestView>(IntensityStationBody()).EarthquakeBody.Intensity!.Observation!.Prefs[0].Areas[0].Cities[0].IntensityStations[0];
		Assert.Equal("name", node.Name);
		Assert.Equal("code", node.Code);
		Assert.Equal("int", node.Int);
		Assert.Equal("rev", node.Revise);
		Assert.NotNull(node.Sva);
		Assert.Single(node.LgIntPerPeriods);
		Assert.Single(node.SvaPerPeriods);
	}
}
