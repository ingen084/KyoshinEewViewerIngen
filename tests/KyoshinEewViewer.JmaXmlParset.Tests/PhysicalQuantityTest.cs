using KyoshinEewViewer.JmaXmlParser;
using System;
using StringLiteral;
using Xunit;
using U8Xml;
using KyoshinEewViewer.JmaXmlParser.Data;

namespace KyoshinEewViewer.JmaXmlParset.Tests;

public partial class PhysicalQuantityTest
{
	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Pressure type=\"気圧\" unit=\"hPa\" description=\"1007ヘクトパスカル\">1007</Pressure>")]
	private static partial ReadOnlySpan<byte> Pressure1();

	[Fact]
	public void ParsePressureNode()
	{
		using var doc = XmlParser.Parse(Pressure1());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("Pressure", node.Name);
		Assert.Equal("気圧", node.Type);
		Assert.Equal("hPa", node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("1007ヘクトパスカル", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetIntValue(out var v));
		Assert.Equal(1007, v);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Pressure type=\"気圧\" unit=\"hPa\" condition=\"不明\" description=\"気圧不明\" />")]
	private static partial ReadOnlySpan<byte> Pressure2();

	[Fact]
	public void ParseUnknownPressureNode()
	{
		using var doc = XmlParser.Parse(Pressure2());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("Pressure", node.Name);
		Assert.Equal("気圧", node.Type);
		Assert.Equal("hPa", node.Unit);
		Assert.Equal("不明", node.Condition);
		Assert.Equal("気圧不明", node.Description);
		Assert.Null(node.RefId);
		Assert.False(node.TryGetIntValue(out var v));
		Assert.Empty(node.Value);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Temperature type=\"最低気温\" unit=\"度\" refID=\"1\">-5</Temperature>")]
	private static partial ReadOnlySpan<byte> Temperature();

	[Fact]
	public void ParseTemperatureNode()
	{
		using var doc = XmlParser.Parse(Temperature());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("Temperature", node.Name);
		Assert.Equal("最低気温", node.Type);
		Assert.Equal("度", node.Unit);
		Assert.Null(node.Condition);
		Assert.Null(node.Description);
		Assert.Equal(1, node.RefId);
		Assert.True(node.TryGetIntValue(out var v));
		Assert.Equal(-5, v);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><WindDirection type=\"風向\" unit=\"１６方位漢字\">北東</WindDirection>")]
	private static partial ReadOnlySpan<byte> WindDirection();

	[Fact]
	public void ParseWindDirectionNode()
	{
		using var doc = XmlParser.Parse(WindDirection());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("WindDirection", node.Name);
		Assert.Equal("風向", node.Type);
		Assert.Equal("１６方位漢字", node.Unit);
		Assert.Null(node.Condition);
		Assert.Null(node.Description);
		Assert.Null(node.RefId);
		Assert.False(node.TryGetIntValue(out var v));
		Assert.Equal("北東", node.Value);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Magnitude type=\"Mj\" description=\"M6.6\">6.6</Magnitude>")]
	private static partial ReadOnlySpan<byte> Magnitude1();

	[Fact]
	public void ParseMagnitudeNode()
	{
		using var doc = XmlParser.Parse(Magnitude1());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("Magnitude", node.Name);
		Assert.Equal("Mj", node.Type);
		Assert.Null(node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("M6.6", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetFloatValue(out var v));
		Assert.Equal(6.6f, v);
	}

	[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Magnitude type=\"Mj\" description=\"M 不明\">NaN</Magnitude>")]
	private static partial ReadOnlySpan<byte> Magnitude2();

	[Fact]
	public void ParseMagnitudeUnknownNode()
	{
		using var doc = XmlParser.Parse(Magnitude2());
		var node = new PhysicalQuantity(doc.Root);
		Assert.Equal("Magnitude", node.Name);
		Assert.Equal("Mj", node.Type);
		Assert.Null(node.Unit);
		Assert.Null(node.Condition);
		Assert.Equal("M 不明", node.Description);
		Assert.Null(node.RefId);
		Assert.True(node.TryGetFloatValue(out var v));
		Assert.True(float.IsNaN(v));
	}
}
