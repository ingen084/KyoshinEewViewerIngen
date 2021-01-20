using System;

namespace KyoshinEewViewerIngen.Dmdata.Exceptions
{
	public class DmdataApiTimeoutException : DmdataException
	{
		public DmdataApiTimeoutException(string message) : base(message)
		{
		}
	}
}
