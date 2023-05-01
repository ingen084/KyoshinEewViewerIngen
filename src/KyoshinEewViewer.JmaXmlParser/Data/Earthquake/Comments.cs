using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Comments
{
	private XmlNode Node { get; set; }

	public Comments(XmlNode node)
	{
		Node = node;
	}

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
}
