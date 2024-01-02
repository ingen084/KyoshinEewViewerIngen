namespace KyoshinEewViewer.DCReportParser.Exceptions;

public class ChecksumErrorException(string? message) : DCReportParseException(message)
{
}
