using KyoshinEewViewer.JmaXmlParser.Data;
using System;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests;

public partial class PhysicalQuantityTest
{
	// PhysicalQuantity は要素名によらず同じ読み方をするので、マグニチュードの位置に置いて読む
	private static PhysicalQuantity<TestView> Read(ReadOnlySpan<byte> body)
		=> JmaXml.Read<TestView>(body).EarthquakeBody.Earthquake!.Magnitude;

	private static ReadOnlySpan<byte> Pressure1() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"気圧\" unit=\"hPa\" description=\"1007ヘクトパスカル\">1007</jmx_eb:Magnitude></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParsePressureNode()
	{
		var node = Read(Pressure1());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("気圧", node.Type);
		Assert.Equal("hPa", node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("1007ヘクトパスカル", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetIntValue(out var v));
		Assert.Equal(1007, v);
	}

	private static ReadOnlySpan<byte> Pressure2() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"気圧\" unit=\"hPa\" condition=\"不明\" description=\"気圧不明\" /></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParseUnknownPressureNode()
	{
		var node = Read(Pressure2());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("気圧", node.Type);
		Assert.Equal("hPa", node.Unit);
		Assert.Equal("不明", node.Condition);
		Assert.Equal("気圧不明", node.Description);
		Assert.Null(node.RefId);
		Assert.False(node.TryGetIntValue(out var v));
		Assert.Empty(node.Value);
	}

	private static ReadOnlySpan<byte> Temperature() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"最低気温\" unit=\"度\" refID=\"1\">-5</jmx_eb:Magnitude></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParseTemperatureNode()
	{
		var node = Read(Temperature());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("最低気温", node.Type);
		Assert.Equal("度", node.Unit);
		Assert.Null(node.Condition);
		Assert.Null(node.Description);
		Assert.Equal(1, node.RefId);
		Assert.True(node.TryGetIntValue(out var v));
		Assert.Equal(-5, v);
	}

	private static ReadOnlySpan<byte> WindDirection() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"風向\" unit=\"１６方位漢字\">北東</jmx_eb:Magnitude></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParseWindDirectionNode()
	{
		var node = Read(WindDirection());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("風向", node.Type);
		Assert.Equal("１６方位漢字", node.Unit);
		Assert.Null(node.Condition);
		Assert.Null(node.Description);
		Assert.Null(node.RefId);
		Assert.False(node.TryGetIntValue(out var v));
		Assert.Equal("北東", node.Value);
	}

	private static ReadOnlySpan<byte> Magnitude1() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"Mj\" description=\"M6.6\">6.6</jmx_eb:Magnitude></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParseMagnitudeNode()
	{
		var node = Read(Magnitude1());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("Mj", node.Type);
		Assert.Null(node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("M6.6", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetFloatValue(out var v));
		Assert.Equal(6.6f, v);
	}

	private static ReadOnlySpan<byte> Magnitude2() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Report><Body><Earthquake><jmx_eb:Magnitude type=\"Mj\" description=\"M 不明\">NaN</jmx_eb:Magnitude></Earthquake></Body></Report>"u8;

	[Fact]
	public void ParseMagnitudeUnknownNode()
	{
		var node = Read(Magnitude2());
		Assert.Equal("jmx_eb:Magnitude", node.Name);
		Assert.Equal("Mj", node.Type);
		Assert.Null(node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("M 不明", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetFloatValue(out var v));
		Assert.True(float.IsNaN(v));
	}
}
