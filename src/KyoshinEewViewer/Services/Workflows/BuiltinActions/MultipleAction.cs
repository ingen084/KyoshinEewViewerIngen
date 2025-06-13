using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ZLinq;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class MultipleAction : WorkflowAction
{
	public override Control DisplayControl => new MultipleActionControl() { DataContext = this };

	public bool IsParallel { get; set; }

	public ObservableCollection<ChildAction> ChildActions { get; set; } = [];

	public void AddAction() => ChildActions.Add(new ChildAction() { Action = new DummyAction() });
	public void RemoveAction(ChildAction action) => ChildActions.Remove(action);

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
}
