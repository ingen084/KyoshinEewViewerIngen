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
}
