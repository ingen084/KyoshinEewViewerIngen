using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using Splat;

namespace KyoshinEewViewer.Desktop.Services;

public class UnixInterProcessCommunicationService : IInterProcessCommunicationService
{
	private static readonly string SocketPath = Path.Combine(Path.GetTempPath(), "KyoshinEewViewerIngen.sock");
	private Socket? _serverSocket;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly ILogger _logger;

	public UnixInterProcessCommunicationService()
	{
		var logManager = Locator.Current.GetService<ILogManager>();
		_logger = logManager?.GetLogger<UnixInterProcessCommunicationService>() ?? throw new InvalidOperationException("LogManagerが見つかりません");
	}

	public void StartServer()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return;

		_cancellationTokenSource = new CancellationTokenSource();
		Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
	}

	private async Task RunServerAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug($"IPCサーバーを開始します。ソケットパス: {SocketPath}");
		
		// 既存のソケットファイルを削除
		if (File.Exists(SocketPath))
			File.Delete(SocketPath);

		_serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		_serverSocket.Bind(new UnixDomainSocketEndPoint(SocketPath));
		_serverSocket.Listen(1);
		
		_logger.LogDebug("IPCサーバーが起動しました");

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var clientSocket = await AcceptAsync(_serverSocket, cancellationToken);
				if (clientSocket == null)
					break;

				using (clientSocket)
				{
					var buffer = new byte[256];
					var received = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
					var message = Encoding.UTF8.GetString(buffer, 0, received);

					if (message == "SHOW_MAIN_WINDOW")
					{
						_logger.LogInfo("別のインスタンスからメインウィンドウ表示要求を受信しました");
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							MessageBus.Current.SendMessage(new ShowMainWindowRequested());
						});
					}
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "IPCサーバーでエラーが発生しました");
			}
		}
	}

	private async Task<Socket?> AcceptAsync(Socket socket, CancellationToken cancellationToken)
	{
		using (cancellationToken.Register(() => socket.Close()))
		{
			try
			{
				return await Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
			}
			catch (ObjectDisposedException)
			{
				return null;
			}
		}
	}

	public async Task<bool> SendShowMainWindowMessageAsync()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return false;

		_logger.LogDebug($"既存インスタンスに通知を送信します。ソケットパス: {SocketPath}");

		try
		{
			using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath));
			
			var message = Encoding.UTF8.GetBytes("SHOW_MAIN_WINDOW");
			await clientSocket.SendAsync(message, SocketFlags.None);
			
			_logger.LogDebug("既存インスタンスへの通知が成功しました");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "既存インスタンスへの通知に失敗しました");
			return false;
		}
	}

	public void Dispose()
	{
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		_serverSocket?.Close();
		_serverSocket?.Dispose();
		
		// ソケットファイルを削除
		if (File.Exists(SocketPath))
		{
			try { File.Delete(SocketPath); } catch { }
		}
		
		GC.SuppressFinalize(this);
	}
}