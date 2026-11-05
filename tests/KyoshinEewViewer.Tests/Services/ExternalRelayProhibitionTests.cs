using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;

namespace KyoshinEewViewer.Tests.Services;

public class ExternalRelayProhibitionTests
{
	[Fact(DisplayName = "EEW Client 由来の緊急地震速報は利用条件により Webhook 送信・ファイル実行アクションで使用されない")]
	public async Task EEWClient由来の緊急地震速報は利用条件によりWebhook送信とファイル実行アクションで使用されない()
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

		// 実行が行われればファイルが存在せず例外の結果になるため、スキップの結果であることを確認する
		var executeFile = new ExecuteFileAction { FilePath = "invalid-file-path", UseShellExecute = false, WaitToEnd = true };
		await executeFile.ExecuteAsync(prohibited);
		Assert.Contains("スキップ", executeFile.LatestExecuteResult);
		Assert.DoesNotContain("例外", executeFile.LatestExecuteResult);

		// 他の受信元は制限の対象にならない
		foreach (var source in new[] { EewSource.KyoshinMonitor, EewSource.SignalNowProfessional, EewSource.Dmdata, EewSource.Axis })
		{
			var allowed = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with { Source = source }, false);
			Assert.False(allowed.IsExternalRelayProhibited);
		}
	}
}
