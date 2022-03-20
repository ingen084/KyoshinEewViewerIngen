using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser;

internal static class Extensions
{
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
		// xsi:nil=true がついていればパース成功でnullとして返す
		if (cn.TryFindAttribute(Literals.Nil(), out var nn) && nn.Value == Literals.True())
			return true;
		if (!DateTimeOffset.TryParse(cn.InnerText.ToString(), out var dt))
			return false;
		result = dt;
		return true;
	}
}
