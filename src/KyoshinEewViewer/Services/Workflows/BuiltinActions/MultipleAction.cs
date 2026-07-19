using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class MultipleAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new MultipleActionControl() { DataContext = this };

	public bool IsParallel { get; set; }

	public ObservableCollection<ChildAction> ChildActions { get; set; } = [];

	public void AddAction() => ChildActions.Add(new ChildAction() { Action = new DummyAction() });
	public async void RemoveAction(object? parameter)
	{
		if (parameter is not ChildAction action)
			return;
		var result = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
			"アクションの削除",
			$"アクション「{action.SelectedActionInfo?.DisplayName}」を削除しますか？\nこの操作は元に戻すことができません。");
		
		if (result)
		{
			ChildActions.Remove(action);
		}
	}
	
	public void MoveActionUp(object? parameter)
	{
		if (parameter is not ChildAction action)
			return;
		var index = ChildActions.IndexOf(action);
		if (index > 0)
			ChildActions.Move(index, index - 1);
	}

	public void MoveActionDown(object? parameter)
	{
		if (parameter is not ChildAction action)
			return;
		var index = ChildActions.IndexOf(action);
		if (index < ChildActions.Count - 1)
			ChildActions.Move(index, index + 1);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		if (IsParallel)
		{
			await Task.WhenAll(ChildActions.Where(a => a.Action != null).Select(async a =>
			{
				await a.Action!.PrepareAsync(content);
				await a.Action!.ExecuteAsync(content);
			}).ToArray());
			return;
		}
		
		// 順次実行：全アクションのPrepareを先行実行
		var prepareTasks = ChildActions
			.Where(x => x.Action != null)
			.Select((a, index) => new { a.Action, Index = index, Task = a.Action!.PrepareAsync(content) })
			.ToArray();
		
		// 各アクションを順次実行（対応するPrepare完了を待機）
		foreach (var item in prepareTasks)
		{
			await item.Task;
			await item.Action!.ExecuteAsync(content);
		}
	}
}

public class ChildAction : ReactiveObject
{
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

	public ChildAction()
	{
		this.WhenAnyValue(x => x.Action).Subscribe(x => _selectedActionInfo = WorkflowService.AllActions.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedActionInfo)
			.Where(x => Action?.GetType() != x?.Type)
			.Subscribe(x => Action = x?.Create());
	}

	// アクション選択時の確認ダイアログ処理
	public async Task SetActionInfo(object? parameter)
	{
		if (parameter is not WorkflowActionInfo actionInfo)
			return;
		// 既にアクションが設定されている場合は確認ダイアログを表示
		if (SelectedActionInfo != null && SelectedActionInfo != actionInfo && Action?.GetType() != typeof(DummyAction))
		{
			var confirmed = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
				"アクション変更の確認",
				$"現在の設定「{SelectedActionInfo.DisplayName}」から「{actionInfo.DisplayName}」に変更しますか？\n\n変更すると現在のアクションに設定されている内容は失われます。");

			if (!confirmed)
				return;
		}

		SelectedActionInfo = actionInfo;
	}
}
