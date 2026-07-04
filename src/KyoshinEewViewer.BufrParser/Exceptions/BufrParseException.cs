namespace KyoshinEewViewer.BufrParser.Exceptions;

/// <summary>
/// BUFR電文の構造やビット内容が仕様と一致しない場合に送出される例外。
/// </summary>
public class BufrParseException : Exception
{
	public BufrParseException(string message) : base(message)
	{
	}

	public BufrParseException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
