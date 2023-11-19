namespace KyoshinEewViewer.DCReportParser.Exceptions;

public class DCReportParseException : Exception
{
	public DCReportParseException(string? message) : base(message)
	{
	}

	public DCReportParseException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}
