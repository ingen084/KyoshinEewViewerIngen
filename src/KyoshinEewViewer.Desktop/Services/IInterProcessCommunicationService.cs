using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.Services;

public interface IInterProcessCommunicationService : IDisposable
{
	void StartServer();
	Task<bool> SendShowMainWindowMessageAsync();
}