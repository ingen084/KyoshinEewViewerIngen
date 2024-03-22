using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows;

public class Workflow : ReactiveObject
{
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
		this.WhenAnyValue(x => x.Trigger).Subscribe(x => _selectedTriggerInfo = AllTriggers.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedTriggerInfo)
			.Where(x => Trigger?.GetType() != x?.Type)
			.Subscribe(x => Trigger = x?.Create());

		this.WhenAnyValue(x => x.Action).Subscribe(x => _selectedActionInfo = AllActions.FirstOrDefault(t => t.Type == x?.GetType()));
		this.WhenAnyValue(x => x.SelectedActionInfo)
			.Where(x => Action?.GetType() != x?.Type)
			.Subscribe(x => Action = x?.Create());
	}
}
