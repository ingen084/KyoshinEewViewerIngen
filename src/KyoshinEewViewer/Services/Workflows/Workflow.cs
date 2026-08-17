using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using R3;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows;

public partial class Workflow : ObservableObject
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[ObservableProperty]
	public partial string Name { get; set; } = "";

	[ObservableProperty]
	public partial bool Enabled { get; set; } = true;

	// Trigger からの同期時は通知を伴わずに代入する必要があるため、
	// バッキングフィールドへアクセスできるフィールド形式で宣言する
	[ObservableProperty]
	[property: JsonIgnore]
	private WorkflowTriggerInfo? _selectedTriggerInfo;

	[ObservableProperty]
	public partial WorkflowTrigger? Trigger { get; set; }

	private MultipleAction _actions = new();
	/// <summary>
	/// ワークフローで実行されるアクション群。常に <see cref="MultipleAction"/> 固定で、
	/// 個々のアクションは <see cref="MultipleAction.ChildActions"/> に格納される。
	/// </summary>
	public MultipleAction Actions
	{
		get => _actions;
		set => SetProperty(ref _actions, value ?? new MultipleAction());
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
		this.ObservePropertyChanged(x => x.Trigger).Subscribe(x => _selectedTriggerInfo = WorkflowService.AllTriggers.FirstOrDefault(t => t.Type == x?.GetType()));
		this.ObservePropertyChanged(x => x.SelectedTriggerInfo)
			.Where(x => Trigger?.GetType() != x?.Type)
			.Subscribe(x => Trigger = x?.Create());
	}

	[JsonIgnore]
	[ObservableProperty]
	public partial bool IsTestRunning { get; set; } = false;

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
