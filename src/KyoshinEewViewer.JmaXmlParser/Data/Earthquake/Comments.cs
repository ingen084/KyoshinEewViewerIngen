using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Comments
{
	private XmlNode Node { get; set; }

	public Comments(XmlNode node)
	{
		Node = node;
	}

	private string? forecastCommentText = null;
	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	public string? ForecastCommentText
	{
		get {
			if (forecastCommentText != null)
				return forecastCommentText;
			if (!Node.TryFindChild(Literals.ForecastComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return forecastCommentText = r;
		}
	}

	private string? forecastCommentCode = null;
	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	public string? ForecastCommentCode
	{
		get {
			if (forecastCommentCode != null)
				return forecastCommentCode;
			if (!Node.TryFindChild(Literals.ForecastComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return forecastCommentCode = r;
		}
	}

	private string? warningCommentText = null;
	/// <summary>
	/// 固定付加文(本文)
	/// </summary>
	public string? WarningCommentText
	{
		get {
			if (warningCommentText != null)
				return warningCommentText;
			if (!Node.TryFindChild(Literals.WarningComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return warningCommentText = r;
		}
	}

	private string? warningCommentCode = null;
	/// <summary>
	/// 固定付加文(コード)
	/// </summary>
	public string? WarningCommentCode
	{
		get {
			if (warningCommentCode != null)
				return warningCommentCode;
			if (!Node.TryFindChild(Literals.WarningComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return warningCommentCode = r;
		}
	}

	private string? varCommentText = null;
	/// <summary>
	/// 固定付加文(その他･本文)
	/// </summary>
	public string? VarCommentText
	{
		get {
			if (varCommentText != null)
				return varCommentText;
			if (!Node.TryFindChild(Literals.VarComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Text(), out var r))
				throw new JmaXmlParseException("Text ノードが存在しません");
			return varCommentText = r;
		}
	}

	private string? varCommentCode = null;
	/// <summary>
	/// 固定付加文(その他･コード)
	/// </summary>
	public string? VarCommentCode
	{
		get {
			if (varCommentCode != null)
				return varCommentCode;
			if (!Node.TryFindChild(Literals.VarComment(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.Code(), out var r))
				throw new JmaXmlParseException("Code ノードが存在しません");
			return varCommentCode = r;
		}
	}

	private string? freeFormComment = null;
	/// <summary>
	/// 自由付加文
	/// </summary>
	public string? FreeFormComment => freeFormComment ??= (Node.TryFindStringNode(Literals.FreeFormComment(), out var n) ? n : null);
}
