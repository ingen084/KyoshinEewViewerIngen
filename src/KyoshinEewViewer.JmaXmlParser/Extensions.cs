using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser;

internal static class Extensions
{
	public static bool IsElementBasis(this XmlNode node, ReadOnlySpan<byte> localName)
		=> node.TryGetFullName(out var ns, out var name) &&
			ns == "http://xml.kishou.go.jp/jmaxml1/elementBasis1/"u8 && name == localName;

	public static bool TryFindElementBasis(this XmlNode node, ReadOnlySpan<byte> localName, out XmlNode result)
	{
		foreach (var child in node.Children)
			if (child.IsElementBasis(localName))
			{
				result = child;
				return true;
			}
		result = default;
		return false;
	}

	public static bool TryFindStringElementBasis(this XmlNode node, ReadOnlySpan<byte> localName, out string result)
	{
		if (!node.TryFindElementBasis(localName, out var child))
		{
			result = null!;
			return false;
		}
		result = child.InnerText.ToString();
		return true;
	}

	public static bool TryFindStringNode(this XmlNode node, ReadOnlySpan<byte> name, out string result)
	{
		if (!node.TryFindChild(name, out var cn))
		{
			result = null!;
			return false;
		}
		result = cn.InnerText.ToString();
		return true;
	}

	public static bool TryFindIntNode(this XmlNode node, ReadOnlySpan<byte> name, out int result)
	{
		if (!node.TryFindChild(name, out var cn) || !cn.InnerText.TryToInt32(out result))
		{
			result = default;
			return false;
		}
		return true;
	}

	public static bool TryFindStringAttribute(this XmlNode node, ReadOnlySpan<byte> name, out string result)
	{
		if (!node.TryFindAttribute(name, out var a))
		{
			result = null!;
			return false;
		}
		result = a.Value.ToString();
		return true;
	}

	public static bool TryFindFloatNode(this XmlNode node, ReadOnlySpan<byte> name, out float result)
	{
		result = default;
		return node.TryFindChild(name, out var child) && child.InnerText.TryToFloat32(out result);
	}

	public static bool TryFindFloatAttribute(this XmlNode node, ReadOnlySpan<byte> name, out float result)
	{
		result = default;
		return node.TryFindAttribute(name, out var attribute) && attribute.Value.TryToFloat32(out result);
	}

	public static bool TryFindIntAttribute(this XmlNode node, ReadOnlySpan<byte> name, out int result)
	{
		if (!node.TryFindAttribute(name, out var a) || !a.Value.TryToInt32(out result))
		{
			result = default;
			return false;
		}
		return true;
	}

	public static bool TryFindDateTimeNode(this XmlNode node, ReadOnlySpan<byte> name, out DateTimeOffset result)
	{
		if (!node.TryFindChild(name, out var cn) || !DateTimeOffset.TryParse(cn.InnerText.ToString(), out result))
		{
			result = default;
			return false;
		}
		return true;
	}

	public static bool TryFindNullableDateTimeNode(this XmlNode node, ReadOnlySpan<byte> name, out DateTimeOffset? result)
	{
		result = null;
		if (!node.TryFindChild(name, out var cn))
			return false;
		// 接頭辞ではなく名前空間 URI で xsi:nil を判別する。
		foreach (var attribute in cn.Attributes)
			if (attribute.TryGetFullName(out var ns, out var attributeName) &&
				ns == "http://www.w3.org/2001/XMLSchema-instance"u8 && attributeName == "nil"u8)
			{
				var value = attribute.Value.AsSpan().Trim(" \t\r\n"u8);
				if (value.SequenceEqual("true"u8) || value.SequenceEqual("1"u8))
					return true;
			}
		if (!DateTimeOffset.TryParse(cn.InnerText.ToString(), out var dt))
			return false;
		result = dt;
		return true;
	}
}
