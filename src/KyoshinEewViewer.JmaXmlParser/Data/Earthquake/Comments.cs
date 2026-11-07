using System;
using System.Text;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Comments(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _forecastCommentText = null;
	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	public string? ForecastCommentText
	{
		get {
			if (_forecastCommentText != null)
				return _forecastCommentText;
			if (!Node.TryFindChild(Literals.ForecastComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return _forecastCommentText = r;
		}
	}

	private string? _forecastCommentCode = null;
	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	public string? ForecastCommentCode
	{
		get {
			if (_forecastCommentCode != null)
				return _forecastCommentCode;
			if (!Node.TryFindChild(Literals.ForecastComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return _forecastCommentCode = r;
		}
	}

	private string? _warningCommentText = null;
	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	public string? WarningCommentText
	{
		get {
			if (_warningCommentText != null)
				return _warningCommentText;
			if (!Node.TryFindChild(Literals.WarningComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return _warningCommentText = r;
		}
	}

	private string? _warningCommentCode = null;
	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	public string? WarningCommentCode
	{
		get {
			if (_warningCommentCode != null)
				return _warningCommentCode;
			if (!Node.TryFindChild(Literals.WarningComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return _warningCommentCode = r;
		}
	}

	private string? _varCommentText = null;
	/// <summary>
	/// 固定付加文(その他･本文)
	/// </summary>
	public string? VarCommentText
	{
		get {
			if (_varCommentText != null)
				return _varCommentText;
			if (!Node.TryFindChild(Literals.VarComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return _varCommentText = r;
		}
	}

	private string? _varCommentCode = null;
	/// <summary>
	/// 固定付加文(その他･コード)
	/// </summary>
	public string? VarCommentCode
	{
		get {
			if (_varCommentCode != null)
				return _varCommentCode;
			if (!Node.TryFindChild(Literals.VarComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return _varCommentCode = r;
		}
	}

	private string? _freeFormComment = null;
	/// <summary>
	/// 自由付加文
	/// </summary>
	public string? FreeFormComment => _freeFormComment ??= (Node.TryFindStringNode(Literals.FreeFormComment(), out var n) ? n : null);

	/// <summary>
	/// 観測に関する固定付加文の本文。存在しない場合は null。
	/// </summary>
	public string? ObservationCommentText => GetCommentValue("ObservationComment"u8, Literals.Text());

	/// <summary>
	/// 観測に関する固定付加文のコード。存在しない場合は null。
	/// </summary>
	public string? ObservationCommentCode => GetCommentValue("ObservationComment"u8, Literals.Code());

	/// <summary>
	/// 固定付加文のコード種別。付加文が存在しない場合は null。
	/// </summary>
	public string? ForecastCommentCodeType => GetCommentCodeType(Literals.ForecastComment());

	/// <summary>
	/// 警告の固定付加文のコード種別。付加文が存在しない場合は null。
	/// </summary>
	public string? WarningCommentCodeType => GetCommentCodeType(Literals.WarningComment());

	/// <summary>
	/// 観測の固定付加文のコード種別。付加文が存在しない場合は null。
	/// </summary>
	public string? ObservationCommentCodeType => GetCommentCodeType("ObservationComment"u8);

	/// <summary>
	/// その他の固定付加文のコード種別。付加文が存在しない場合は null。
	/// </summary>
	public string? VarCommentCodeType => GetCommentCodeType(Literals.VarComment());

	private string? _uri = null;
	/// <summary>
	/// 関連資料の URI。存在しない場合は null。
	/// </summary>
	public string? Uri => _uri ??= (Node.TryFindStringNode("URI"u8, out var n) ? n : null);

	private string? GetCommentValue(ReadOnlySpan<byte> name, ReadOnlySpan<byte> childName)
	{
		if (!Node.TryFindChild(name, out var n))
			return null;
		return n.TryFindStringNode(childName, out var value) ? value : throw new JmaXmlParseException($"{Encoding.UTF8.GetString(childName)} ノードが存在しません");
	}

	private string? GetCommentCodeType(ReadOnlySpan<byte> name)
	{
		if (!Node.TryFindChild(name, out var n))
			return null;
		return n.TryFindStringAttribute(Literals.AttrCodeType(), out var value) ? value : throw new JmaXmlParseException("codeType 属性が存在しません");
	}
}
