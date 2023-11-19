using System;

namespace KyoshinEewViewer.JmaXmlParser;

public class JmaXmlParseException(string message, Exception? innerException = null) : Exception(message, innerException)
{
}
