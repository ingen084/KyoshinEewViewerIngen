using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.UiTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.UiTests;

public sealed class EewSettingUiTests
{
	[AvaloniaFact]
	public void 表示中の訓練を緊急地震速報の設定から終了できる()
	{
		using var bindingErrors = new BindingErrorRecorder();
		var config = new KyoshinEewViewerConfiguration();
		var timer = new TimerService(NullLogger<TimerService>.Instance, config);
		var controller = new EewController(
			NullLogger<EewController>.Instance,
			null!,
			config,
			new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance),
			new WorkflowService(NullLogger<WorkflowService>.Instance));
		var training = EewMock.NORMAL with { Id = "20261108120000", IsTraining = true, ReceiveTime = timer.CurrentTime };

		var page = new KyoshinMonitorEewSettingPage(config, controller, timer);
		var window = new Window { Width = 800, Height = 1000, Content = page.DisplayControl };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var button = Ui.FindButton(window, "終了する");
			Assert.False(button.IsEffectivelyEnabled);

			controller.Update(training, timer.CurrentTime);
			Dispatcher.UIThread.RunJobs();
			Assert.True(button.IsEffectivelyEnabled);

			Ui.Click(window, button);
			Dispatcher.UIThread.RunJobs();
			Assert.False(controller.TryGetEew(training.Key, out _));
			Assert.False(button.IsEffectivelyEnabled);

			Assert.Empty(bindingErrors.Errors);
		}
		finally { window.Close(); }
	}
}
