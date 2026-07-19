using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using ReactiveUI;
using System;
using System.ComponentModel;
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

	private MultipleAction _actions = new();
	/// <summary>
	/// ワークフローで実行されるアクション群。常に <see cref="MultipleAction"/> 固定で、
	/// 個々のアクションは <see cref="MultipleAction.ChildActions"/> に格納される。
	/// </summary>
	public MultipleAction Actions
	{
		get => _actions;
		set => this.RaiseAndSetIfChanged(ref _actions, value ?? new MultipleAction());
	}

	/// <summary>
	/// 旧バージョンの workflows.json との互換のための JSON 読み込み専用プロパティ。
	/// 旧形式の "Action" フィールドを <see cref="Actions"/> へ自動変換する。
	/// 既に <see cref="MultipleAction"/> だった場合はそのまま代入し、それ以外は
	/// <see cref="MultipleAction"/> でラップして 1 件だけ含める形に変換する。
	/// 新規コードからは使用せず、必ず <see cref="Actions"/> を使用すること。
	/// </summary>
	[JsonPropertyName("Action")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("旧形式 workflows.json 互換のためのレガシープロパティ。代わりに Actions を使用してください。", error: false)]
	public WorkflowAction? Action
	{
		get => null;
		set
		{
			if (value == null)
				return;
			if (value is MultipleAction multi)
			{
				Actions = multi;
				return;
			}
			Actions = new MultipleAction
			{
				ChildActions = { new ChildAction { Action = value } }
			};
		}
	}

	public Workflow()
	{
		this.WhenAnyValue(x => x.Trigger).Subscribe(x => _selectedTriggerInfo = WorkflowService.AllTriggers.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedTriggerInfo)
			.Where(x => Trigger?.GetType() != x?.Type)
			.Subscribe(x => Trigger = x?.Create());
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
		if (Trigger == null)
			return Task.CompletedTask;
		return Actions.ExecuteAsync(Trigger.CreateTestEvent());
	}

	public Task ExecuteAsync(WorkflowEvent content)
	{
		if (Trigger == null || !Trigger.CheckTrigger(content))
			return Task.CompletedTask;
		return Actions.ExecuteAsync(content);
	}

	// トリガー選択時の確認ダイアログ処理
	public async Task SetTriggerInfo(object? parameter)
	{
		if (parameter is not WorkflowTriggerInfo triggerInfo)
			return;
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
}
