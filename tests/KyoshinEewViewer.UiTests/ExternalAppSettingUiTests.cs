using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.UiTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.UiTests;

public sealed class ExternalAppSettingUiTests
{
	[AvaloniaFact]
	public void 外部アプリ連携の設定ページにEEWClientの設定と接続状態が表示される()
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
		using var receiver = new DmdataEewClientReceiver(NullLogger<DmdataEewClientReceiver>.Instance, config, controller, timer);
		receiver.Start();

		var page = new ExternalAppSettingPage(config, receiver);
		var window = new Window { Width = 800, Height = 1000, Content = page.DisplayControl };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var texts = Ui.VisibleTexts(window);
			Assert.Contains("SignalNow Professional", texts);
			Assert.Contains("接続状態", texts);
			Assert.Contains("EEW Client連携は無効です。", texts);

			var portInput = window.GetVisualDescendants().OfType<NumericUpDown>().Single();
			Assert.False(portInput.IsEffectivelyEnabled);

			// ポート番号が未設定の間は接続を試みず、設定を促す
			config.Eew.DmdataEewClient.Enable = true;
			Dispatcher.UIThread.RunJobs();
			Assert.Contains("EEW Clientの外部連携機能のポート番号を設定してください。", Ui.VisibleTexts(window));
			Assert.True(portInput.IsEffectivelyEnabled);
			Assert.Equal(DmdataEewClientConnectionState.Disconnected, receiver.State);

			Assert.Empty(bindingErrors.Errors);
		}
		finally { window.Close(); }
	}
}
