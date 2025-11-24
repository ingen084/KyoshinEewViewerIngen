using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows;

public class Workflow : ReactiveObject
{
	public Guid Id { get; set; } = Guid.NewGuid();

	private string _name = "";
	public string Name
	{
		get => _name;
		set => this.RaiseAndSetIfChanged(ref _name, value);
	}

	private bool _enabled = true;
	public bool Enabled
	{
		get => _enabled;
		set => this.RaiseAndSetIfChanged(ref _enabled, value);
	}

	private WorkflowTriggerInfo? _selectedTriggerInfo;
	[JsonIgnore]
	public WorkflowTriggerInfo? SelectedTriggerInfo
	{
		get => _selectedTriggerInfo;
		set => this.RaiseAndSetIfChanged(ref _selectedTriggerInfo, value);
	}

	private WorkflowTrigger? _trigger;
	public WorkflowTrigger? Trigger
	{
		get => _trigger;
		set => this.RaiseAndSetIfChanged(ref _trigger, value);
	}

	private WorkflowActionInfo? _selectedActionInfo;
	[JsonIgnore]
	public WorkflowActionInfo? SelectedActionInfo
	{
		get => _selectedActionInfo;
		set => this.RaiseAndSetIfChanged(ref _selectedActionInfo, value);
	}
	private WorkflowAction? _action;
	public WorkflowAction? Action
	{
		get => _action;
		set => this.RaiseAndSetIfChanged(ref _action, value);
	}

	public Workflow()
	{
		this.WhenAnyValue(x => x.Trigger).Subscribe(x => _selectedTriggerInfo = WorkflowService.AllTriggers.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedTriggerInfo)
			.Where(x => Trigger?.GetType() != x?.Type)
			.Subscribe(x => Trigger = x?.Create());

		this.WhenAnyValue(x => x.Action).Subscribe(x => _selectedActionInfo = WorkflowService.AllActions.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedActionInfo)
			.Where(x => Action?.GetType() != x?.Type)
			.Subscribe(x => Action = x?.Create());
	}

	private bool _isTestRunning = false;
	[JsonIgnore]
	public bool IsTestRunning
	{
		get => _isTestRunning;
		set => this.RaiseAndSetIfChanged(ref _isTestRunning, value);
	}

	public Task TestRunAsync()
	{
		if (Trigger == null || Action == null)
			return Task.CompletedTask;
		return Action.ExecuteAsync(Trigger.CreateTestEvent());
	}

	public Task ExecuteAsync(WorkflowEvent content)
	{
		if (Action == null || Trigger == null || !Trigger.CheckTrigger(content))
			return Task.CompletedTask;
		return Action.ExecuteAsync(content);
	}

	// トリガー選択時の確認ダイアログ処理
	public async Task SetTriggerInfo(WorkflowTriggerInfo triggerInfo)
	{
		// 既にトリガーが設定されている場合は確認ダイアログを表示
		if (SelectedTriggerInfo != null && SelectedTriggerInfo != triggerInfo && Trigger?.GetType() != typeof(DummyTrigger))
		{
			var confirmed = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
				"トリガー変更の確認",
				$"現在の設定「{SelectedTriggerInfo.DisplayName}」から「{triggerInfo.DisplayName}」に変更しますか？\n\n変更すると現在設定されているトリガーの内容は失われます。");

			if (!confirmed)
				return;
		}

		SelectedTriggerInfo = triggerInfo;
	}

	// アクション選択時の確認ダイアログ処理
	public async Task SetActionInfo(WorkflowActionInfo actionInfo)
	{
		// 既にアクションが設定されている場合は確認ダイアログを表示
		if (SelectedActionInfo != null && SelectedActionInfo != actionInfo && Action?.GetType() != typeof(DummyAction))
		{
			var confirmed = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
				"アクション変更の確認",
				$"現在の設定「{SelectedActionInfo.DisplayName}」から「{actionInfo.DisplayName}」に変更しますか？\n\n変更すると現在設定されているアクションの内容は失われます。");

			if (!confirmed)
				return;
		}

		SelectedActionInfo = actionInfo;
	}
}
