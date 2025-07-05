using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using Splat;

namespace KyoshinEewViewer.Desktop.Services;

public class WindowsInterProcessCommunicationService : IInterProcessCommunicationService
{
	private const string PipeName = "KyoshinEewViewerIngen_IPC";
	private NamedPipeServerStream? _pipeServer;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly ILogger _logger;

	public WindowsInterProcessCommunicationService()
	{
		var logManager = Locator.Current.GetService<ILogManager>();
		_logger = logManager?.GetLogger<WindowsInterProcessCommunicationService>() ?? throw new InvalidOperationException("LogManagerが見つかりません");
	}

	public void StartServer()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return;

		_cancellationTokenSource = new CancellationTokenSource();
		Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
	}

	private async Task RunServerAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug($"IPCサーバーを開始します。パイプ名: {PipeName}");
		
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				
				await _pipeServer.WaitForConnectionAsync(cancellationToken);
				
				using var reader = new StreamReader(_pipeServer);
				var message = await reader.ReadToEndAsync(cancellationToken);
				
				if (message == "SHOW_MAIN_WINDOW")
				{
					_logger.LogInfo("別のインスタンスからメインウィンドウ表示要求を受信しました");
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						MessageBus.Current.SendMessage(new ShowMainWindowRequested());
					});
				}
				
				_pipeServer.Disconnect();
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "IPCサーバーでエラーが発生しました");
			}
			finally
			{
				_pipeServer?.Dispose();
				_pipeServer = null;
			}
		}
	}

	public async Task<bool> SendShowMainWindowMessageAsync()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return false;

		_logger.LogDebug($"既存インスタンスに通知を送信します。パイプ名: {PipeName}");

		try
		{
			using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
			await pipeClient.ConnectAsync(1000); // 1秒でタイムアウト
			
			using var writer = new StreamWriter(pipeClient);
			await writer.WriteAsync("SHOW_MAIN_WINDOW");
			await writer.FlushAsync();
			
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
		_pipeServer?.Dispose();
		GC.SuppressFinalize(this);
	}
}