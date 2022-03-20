using System;

namespace KyoshinEewViewer.JmaXmlParser;

public class JmaXmlParseException : Exception
{
	public JmaXmlParseException(string message, Exception? innerException = null) : base(message, innerException) { }
}
