using System;

namespace KyoshinEewViewer.Dmdata.Exceptions
{
	public class DmdataApiTimeoutException : DmdataException
	{
		public DmdataApiTimeoutException(string message) : base(message)
		{
		}
	}
}
