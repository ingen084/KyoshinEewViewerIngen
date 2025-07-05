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
			NamedPipeServerStream? currentPipe = null;
			try
			{
				// NOTE: MaxAllowedServerInstancesにしておかないと最小化からの復旧時にパイプが終了されないことがあってエラーになる
				currentPipe = new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);

				await currentPipe.WaitForConnectionAsync(cancellationToken);

				using var reader = new StreamReader(currentPipe);
				var message = await reader.ReadToEndAsync(cancellationToken);

				if (message == "SHOW_MAIN_WINDOW")
				{
					_logger.LogInfo("別のインスタンスからメインウィンドウ表示要求を受信しました");
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						MessageBus.Current.SendMessage(new ShowMainWindowRequested());
					});
				}

				currentPipe.Disconnect();
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "IPCサーバーでエラーが発生しました");
			}
			finally
			{
				currentPipe?.Dispose();
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
			using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
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
		GC.SuppressFinalize(this);
	}
}
