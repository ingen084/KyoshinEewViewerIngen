using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.Workflows;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using Microsoft.Extensions.Logging.Abstractions;
using WorkflowModel = KyoshinEewViewer.Services.Workflows.Workflow;

namespace KyoshinEewViewer.Tests.Services;

public class ExternalRelayProhibitionTests
{
	[Fact(DisplayName = "EEW Client 由来の緊急地震速報は利用条件により Webhook で送信されない")]
	public async Task EEWClient由来の緊急地震速報は利用条件によりWebhookで送信されない()
	{
		var prohibited = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with
		{
			Source = EewSource.DmdataEewClient,
			DisplaySource = "EEW Client(気象庁本庁)",
		}, false);
		Assert.True(prohibited.IsExternalRelayProhibited);

		// 送信が行われれば名前解決に失敗して例外の応答になるため、スキップの応答であることを確認する
		var webhook = new WebhookAction { Url = "http://invalid-endpoint/" };
		await webhook.ExecuteAsync(prohibited);
		Assert.Contains("スキップ", webhook.LatestResponse);
		Assert.DoesNotContain("例外", webhook.LatestResponse);

		// 他の受信元は制限の対象にならない
		foreach (var source in new[] { EewSource.KyoshinMonitor, EewSource.SignalNowProfessional, EewSource.Dmdata, EewSource.Axis })
		{
			var allowed = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with { Source = source }, false);
			Assert.False(allowed.IsExternalRelayProhibited);
		}
	}

	[Fact(DisplayName = "EEW Client 由来の緊急地震速報ではファイル実行にテンプレートで内容を渡し、環境変数には設定しない")]
	public async Task EEWClient由来の緊急地震速報ではファイル実行の環境変数に内容を設定しない()
	{
		// 親プロセスから継承した値も子プロセスへ渡らないことを確認する
		var original = Environment.GetEnvironmentVariable("KEVI_EVENT_DATA");
		Environment.SetEnvironmentVariable("KEVI_EVENT_DATA", "inherited");
		try
		{
			await AssertEnvironmentVariable(EewSource.DmdataEewClient, false);
			await AssertEnvironmentVariable(EewSource.Dmdata, true);
		}
		finally
		{
			Environment.SetEnvironmentVariable("KEVI_EVENT_DATA", original);
		}
	}

	private static async Task AssertEnvironmentVariable(EewSource source, bool expectEnvironmentVariable)
	{
		var content = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with { Source = source }, false);
		Assert.Equal(!expectEnvironmentVariable, content.IsExternalRelayProhibited);

		var output = Path.Combine(Path.GetTempPath(), $"kevi-test-{Guid.NewGuid():N}.txt");
		try
		{
			// 起動したプロセスからテンプレートの展開結果と環境変数をファイルへ書き出す
			var action = OperatingSystem.IsWindows()
				? new ExecuteFileAction { FilePath = "cmd", Arguments = $"/c echo template:{{{{ EewId }}}} > \"{output}\" & set >> \"{output}\"", UseShellExecute = false, WaitToEnd = true }
				: new ExecuteFileAction { FilePath = "sh", Arguments = $"-c \"echo template:{{{{ EewId }}}} > '{output}'; env >> '{output}'\"", UseShellExecute = false, WaitToEnd = true };
			await action.ExecuteAsync(content);

			var written = await File.ReadAllTextAsync(output);
			Assert.Contains($"template:{content.EewId}", written);
			if (expectEnvironmentVariable)
				Assert.Contains("KEVI_EVENT_DATA={", written);
			else
				Assert.DoesNotContain("KEVI_EVENT_DATA=", written);
		}
		finally
		{
			File.Delete(output);
		}
	}

	[Fact(DisplayName = "EEW Client の取消は他の受信元から受信した EEW に適用しても外部へ送信せず、取消の後に受信した EEW にも制限を引き継ぐ")]
	public void EEWClientの取消は他の受信元のEEWに適用しても外部へ送信しない()
	{
		foreach (var source in new[] { EewSource.KyoshinMonitor, EewSource.SignalNowProfessional, EewSource.Dmdata, EewSource.Axis })
		{
			var (controller, events) = CreateController();

			controller.Update(CreateEew(source, 1), BaseTime);
			Assert.False(Assert.Single(events).IsExternalRelayProhibited);

			events.Clear();
			controller.CancelledWithRelayProhibited(EventId, BaseTime);
			var cancel = Assert.Single(events);
			Assert.Equal(EewEventType.Cancel, cancel.EventSubType);
			Assert.True(cancel.IsExternalRelayProhibited);

			events.Clear();
			controller.Update(CreateEew(source, 2), BaseTime);
			var inherited = Assert.Single(events);
			Assert.True(inherited.IsCancelled);
			Assert.True(inherited.IsExternalRelayProhibited);
		}
	}

	[Fact(DisplayName = "強震モニタの擬似キャンセルは送信でき、後から EEW Client の取消で確定した場合は以降を送信しない")]
	public void 擬似キャンセル後にEEWClientの取消で確定すると以降を送信しない()
	{
		var (controller, events) = CreateController();

		controller.Update(CreateEew(EewSource.KyoshinMonitor, 1), BaseTime);
		events.Clear();
		controller.Cancelled(null, BaseTime.AddSeconds(1));
		Assert.False(Assert.Single(events).IsExternalRelayProhibited);

		events.Clear();
		controller.CancelledWithRelayProhibited(EventId, BaseTime.AddSeconds(2));
		Assert.Empty(events);

		controller.Update(CreateEew(EewSource.KyoshinMonitor, 2), BaseTime.AddSeconds(3));
		var inherited = Assert.Single(events);
		Assert.True(inherited.IsTrueCancelled);
		Assert.True(inherited.IsExternalRelayProhibited);
	}

	[Fact(DisplayName = "他の受信元の取消は送信でき、その後に届く EEW Client の取消で制限を加えない")]
	public void 他の受信元の取消は送信できる()
	{
		var (controller, events) = CreateController();

		controller.Update(CreateEew(EewSource.SignalNowProfessional, 1), BaseTime);
		events.Clear();
		controller.Cancelled(EventId, BaseTime);
		var cancel = Assert.Single(events);
		Assert.Equal(EewEventType.Cancel, cancel.EventSubType);
		Assert.False(cancel.IsExternalRelayProhibited);

		events.Clear();
		controller.CancelledWithRelayProhibited(EventId, BaseTime);
		Assert.Empty(events);

		controller.Update(CreateEew(EewSource.SignalNowProfessional, 2), BaseTime);
		var inherited = Assert.Single(events);
		Assert.True(inherited.IsCancelled);
		Assert.False(inherited.IsExternalRelayProhibited);
	}

	private const string EventId = "20261105120000";
	private static readonly DateTime BaseTime = new(2026, 11, 5, 12, 0, 0);

	private static Eew CreateEew(EewSource source, int serialNo)
		=> EewMock.NORMAL with
		{
			Id = EventId,
			Source = source,
			DisplaySource = source.ToString(),
			SerialNo = serialNo,
			IsWarning = false,
			IsFinal = false,
			IsCancelled = false,
			IsTrueCancelled = false,
			ReceiveTime = BaseTime,
		};

	private static (EewController Controller, List<EewEvent> Events) CreateController()
	{
		var events = new List<EewEvent>();
		var workflowService = new WorkflowService(NullLogger<WorkflowService>.Instance);
		workflowService.UserWorkflows.Add(new WorkflowModel { Trigger = new CaptureTrigger(events) });
		var config = new KyoshinEewViewerConfiguration();
		return (new EewController(
			NullLogger<EewController>.Instance,
			null!,
			config,
			new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance),
			workflowService), events);
	}

	private sealed class CaptureTrigger(List<EewEvent> events) : WorkflowTrigger
	{
		public override Type EventType => typeof(EewEvent);
		public override Control DisplayControl => throw new NotSupportedException();
		public override WorkflowEvent CreateTestEvent() => throw new NotSupportedException();

		public override bool CheckTrigger(WorkflowEvent content)
		{
			if (content is EewEvent e)
				events.Add(e);
			return false;
		}
	}
}
