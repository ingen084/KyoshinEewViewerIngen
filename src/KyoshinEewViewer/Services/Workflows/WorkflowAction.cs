using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowActionInfo(Type Type, string DisplayName, Func<WorkflowAction> Create);

[JsonDerivedType(typeof(DummyAction), typeDiscriminator: "Dummy")]
[JsonDerivedType(typeof(MultipleAction), typeDiscriminator: "Multiple")]
[JsonDerivedType(typeof(SendNotificationAction), typeDiscriminator: "SendNotification")]
[JsonDerivedType(typeof(PlaySoundAction), typeDiscriminator: "PlaySound")]
[JsonDerivedType(typeof(WindowActivateAction), typeDiscriminator: "WindowActivate")]
[JsonDerivedType(typeof(WaitAction), typeDiscriminator: "Wait")]
[JsonDerivedType(typeof(LogOutputAction), typeDiscriminator: "LogOutput")]
[JsonDerivedType(typeof(WebhookAction), typeDiscriminator: "Webhook")]
[JsonDerivedType(typeof(ExecuteFileAction), typeDiscriminator: "ExecuteFile")]
[JsonDerivedType(typeof(VoicevoxSpeechAction), typeDiscriminator: "VoicevoxSpeech")]
[JsonDerivedType(typeof(SwitchTabAction), typeDiscriminator: "SwitchTab")]
public abstract class WorkflowAction : ObservableObject
{
	static WorkflowAction()
	{
		WorkflowService.RegisterAction<DummyAction>("何もしない");
		WorkflowService.RegisterAction<MultipleAction>("複数アクション実行");
		WorkflowService.RegisterAction<SendNotificationAction>("通知送信");
		WorkflowService.RegisterAction<PlaySoundAction>("音声再生");
		WorkflowService.RegisterAction<VoicevoxSpeechAction>("VOICEVOX でテキスト読み上げ");
		WorkflowService.RegisterAction<WindowActivateAction>("メインウィンドウを最前面に表示");
		WorkflowService.RegisterAction<SwitchTabAction>("タブを切り替える");
		WorkflowService.RegisterAction<WaitAction>("指定時間待機");
		WorkflowService.RegisterAction<LogOutputAction>("ログ出力");
		WorkflowService.RegisterAction<WebhookAction>("指定したURLに内容をPOST");
		WorkflowService.RegisterAction<ExecuteFileAction>("指定したファイルを開く(実行)");
	}

	[JsonIgnore]
	public abstract Control DisplayControl { get; }

	public virtual Task PrepareAsync(WorkflowEvent content) => Task.CompletedTask;
	public abstract Task ExecuteAsync(WorkflowEvent content);

	/// <summary>
	/// このアクションが所属するワークフローのトリガーからイベント型を取得する
	/// </summary>
	public Type? FindEventType()
		=> FindWorkflow()?.Trigger?.EventType;

	/// <summary>
	/// このアクションが所属するワークフローを取得する
	/// </summary>
	public Workflow? FindWorkflow()
	{
		var workflowService = ServiceLocator.Current.GetService<WorkflowService>();
		if (workflowService == null)
			return null;
		return workflowService.Workflows.Concat(workflowService.SystemWorkflows)
			.FirstOrDefault(w => w.Actions == this || w.Actions.ChildActions.Any(c => c.Action == this));
	}
}

public class DummyAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new TextBlock { Text = "何もしないアクションです。\n何も実行されず、中断されることもありません。" };
	public override Task ExecuteAsync(WorkflowEvent content)
		=> Task.CompletedTask;
}
