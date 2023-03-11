namespace KyoshinEewViewer.DCReportParser.Exceptions;

public class ChecksumErrorException : DCReportParseException
{
	public ChecksumErrorException(string? message) : base(message)
	{
	}
}
