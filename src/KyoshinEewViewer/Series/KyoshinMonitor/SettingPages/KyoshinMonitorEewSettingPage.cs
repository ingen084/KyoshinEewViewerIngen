using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;

/// <summary>
/// 緊急地震速報の設定ページ 表示中の訓練の終了も行う
/// </summary>
public partial class KyoshinMonitorEewSettingPage : ObservableObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "緊急地震速報";

	public Control DisplayControl => new KyoshinMonitorEewPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	public KyoshinEewViewerConfiguration Config { get; }
	private EewController EewController { get; }
	private TimerService Timer { get; }

	[ObservableProperty]
	public partial bool IsTrainingDisplayed { get; private set; }

	public KyoshinMonitorEewSettingPage(KyoshinEewViewerConfiguration config, EewController eewController, TimerService timer)
	{
		Config = config;
		EewController = eewController;
		Timer = timer;

		EewController.EewUpdated += (_, eews) =>
		{
			var isTrainingDisplayed = eews.Any(e => e.IsTraining);
			Dispatcher.UIThread.Post(() => IsTrainingDisplayed = isTrainingDisplayed);
		};
	}

	public void EndTraining()
		=> EewController.EndTraining(Timer.CurrentTime);
}
