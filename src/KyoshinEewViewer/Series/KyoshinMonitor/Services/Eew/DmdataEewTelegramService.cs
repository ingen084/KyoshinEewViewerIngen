using Avalonia.Controls;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
public class DmdataEewTelegramService : ReactiveObject
{
	private ILogger Logger { get; }
	private EewController EewController { get; }

	private bool _enabled;
	public bool Enabled
	{
		get => _enabled;
		set => this.RaiseAndSetIfChanged(ref _enabled, value);
	}

	public DmdataEewTelegramService(EewController eewControlService, TelegramProvideService telegramProvider)
	{
		EewController = eewControlService;
		Logger = LoggingService.CreateLogger(this);

		if (Design.IsDesignMode)
			return;

		telegramProvider.Subscribe(
			InformationCategory.EewForecast,
			(s, t) =>
			{
				// 有効になった
				Enabled = true;
			},
			async t =>
			{
				// 受信した
				// TODO: ここにパース処理
			},
			isAllFailed =>
			{
				// 死んだ
				Enabled = false;
			});
	}
}
