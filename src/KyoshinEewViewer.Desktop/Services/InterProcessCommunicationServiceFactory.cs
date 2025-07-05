using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Desktop.Services;

public static class InterProcessCommunicationServiceFactory
{
	public static IInterProcessCommunicationService Create()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return new WindowsInterProcessCommunicationService();
		else
			return new UnixInterProcessCommunicationService();
	}
}