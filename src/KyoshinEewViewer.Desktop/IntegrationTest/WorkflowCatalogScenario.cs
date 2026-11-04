#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.VisualTree;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinEewViewer.Views.SettingPages;
using System;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;
using static KyoshinEewViewer.Desktop.IntegrationTest.WorkflowPageOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// すべてのトリガーとアクションを選び、保存と読み戻しで内容が変わらないことを確認する
/// </summary>
/// <remarks>
/// 保存した内容は再起動後の確認に使う。トリガーかアクションのどちらかは必ず「何もしない」にして、
/// ファイルの実行や通信が実際に動かないようにする。
/// 期待値は文字列で持つ。名前の表から読むと、そのメンバーがトリミングされなくなり消失を検出できない
/// </remarks>
internal static class WorkflowCatalogScenario
{
	private sealed record ComboBoxCase(string Default, int NewIndex, string Changed);
	private sealed record TriggerCase(string Name, string Text, ComboBoxCase[] ComboBoxes);
	private sealed record ActionCase(string Name, string Text);

	private static readonly TriggerCase[] Triggers =
	[
		new("何もしない", "何もしないトリガーです。", []),
		new("すべて", "すべてのイベントに対しトリガーされます。", []),
		new("アプリケーション起動時", "アプリケーション起動完了時に一度だけトリガーされます。", []),
		new("アプリケーションの更新存在時", "繰り返しトリガーする", []),
		new("(強震モニタ)揺れ検知", "検知レベル: ", [new("普通(震度1程度以上)", 3, "強い(震度3程度以上)"), new("なにもしない", 1, "地域のみ")]),
		new("(強震モニタ)緊急地震速報", "条件(震度/M)", [new("震度不明", 5, "震度4")]),
		new("(地震情報)地震情報受信", "最大震度: ", [new("震度不明", 6, "震度5弱")]),
		new("(津波情報)津波情報更新時", "発表レベル: ", [new("津波なし(すべて)", 3, "津波警報")]),
		new("(災危通報)受信･更新時", "条件", []),
	];

	// 「複数アクション実行」は中に追加ボタンを持つので最後に追加する
	private static readonly ActionCase[] Actions =
	[
		new("何もしない", "何もしないアクションです。"),
		new("通知送信", "通知タイトル"),
		new("音声再生", "音量:"),
		new("VOICEVOX でテキスト読み上げ", "順次読み上げ"),
		new("メインウィンドウを最前面に表示", "メインウィンドウを最前面に表示します。"),
		new("タブを切り替える", "イベントを発生させたタブに切り替えます。"),
		new("指定時間待機", "待機時間(ミリ秒): "),
		new("ログ出力", "最新の実行結果"),
		new("指定したURLに内容をPOST", "URL"),
		new("指定したファイルを開く(実行)", "実行ファイル: "),
		new("複数アクション実行", "同時実行を行う"),
	];

	public static async Task RunAsync(SettingWindowSession settings)
	{
		if (Triggers.Length != WorkflowService.AllTriggers.Count || Actions.Length != WorkflowService.AllActions.Count)
			throw new InvalidOperationException("シナリオの一覧に登録済みのトリガーかアクションが足りません");

		var page = await settings.ShowPageAsync<WorkflowPage>(PageTitle);
		foreach (var trigger in Triggers)
		{
			UiTestDriver.Step($"トリガー「{trigger.Name}」を選ぶ");
			await AddWorkflowAsync(page);
			await ChooseTriggerAsync(page, trigger.Name);
			await WaitForTextStartingWithAsync(page, trigger.Text);
			foreach (var (comboBox, expected) in (await TriggerComboBoxesAsync(page, trigger)).Zip(trigger.ComboBoxes))
			{
				await WaitForTextAsync(comboBox, expected.Default);
				comboBox.SelectedIndex = expected.NewIndex;
				await WaitForTextAsync(comboBox, expected.Changed);
			}
		}

		await AddWorkflowAsync(page);
		var actions = await WaitForControlAsync<MultipleActionControl>(page);
		foreach (var action in Actions)
		{
			UiTestDriver.Step($"アクション「{action.Name}」を選ぶ");
			await AddActionAsync(actions, action.Name);
			await WaitForTextStartingWithAsync(actions, action.Text);
		}

		UiTestDriver.Step("保存して読み戻す");
		await ClickByTipAsync(page, SaveTip);
		await ClickByTipAsync(page, RestoreTip);
		await VerifyAsync(page);
	}

	public static async Task VerifyAfterRestartAsync(SettingWindowSession settings)
	{
		UiTestDriver.Step("再起動後のワークフローを確認する");
		await VerifyAsync(await settings.ShowPageAsync<WorkflowPage>(PageTitle));
	}

	private static async Task VerifyAsync(WorkflowPage page)
	{
		var selector = await WaitForControlAsync<ComboBox>(page);
		await WaitUntilAsync(() => selector.ItemCount == Triggers.Length + 1, $"ワークフロー {Triggers.Length + 1} 件");

		for (var i = 0; i < Triggers.Length; i++)
		{
			var trigger = Triggers[i];
			UiTestDriver.Step($"トリガー「{trigger.Name}」の保存内容を確認する");
			selector.SelectedIndex = i;
			await WaitForTextAsync(page, trigger.Name);
			await WaitForTextStartingWithAsync(page, trigger.Text);
			foreach (var (comboBox, expected) in (await TriggerComboBoxesAsync(page, trigger)).Zip(trigger.ComboBoxes))
				await WaitForTextAsync(comboBox, expected.Changed);
		}

		UiTestDriver.Step("アクションの保存内容を確認する");
		selector.SelectedIndex = Triggers.Length;
		foreach (var action in Actions)
		{
			await WaitForTextAsync(page, action.Name);
			await WaitForTextStartingWithAsync(page, action.Text);
		}

		UiTestDriver.Step("読み戻した音声再生アクションのボタンを確認する");
		await WaitUntilAsync(
			() => page.GetVisualDescendants().OfType<PlaySoundActionControl>().SelectMany(c => c.GetVisualDescendants().OfType<Button>())
				.Where(b => b.GetType() == typeof(Button)).ToArray() is { Length: > 0 } buttons && buttons.All(b => b.Command != null),
			"音声再生アクションのボタンのコマンド");
	}

	private static Task<ComboBox[]> TriggerComboBoxesAsync(WorkflowPage page, TriggerCase trigger)
		=> WaitForAsync(
			() => page.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible).Skip(1).ToArray() is { } found
				&& found.Length == trigger.ComboBoxes.Length ? found : null,
			$"トリガー「{trigger.Name}」の選択欄");
}
#endif
