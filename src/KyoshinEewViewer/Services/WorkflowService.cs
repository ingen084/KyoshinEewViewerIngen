using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using Scriban.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services;

public class WorkflowService
{
	private static readonly List<WorkflowTriggerInfo> _allTriggers = [];
	public static IReadOnlyList<WorkflowTriggerInfo> AllTriggers => _allTriggers;
	private static readonly List<WorkflowActionInfo> _allActions = [];
	public static IReadOnlyList<WorkflowActionInfo> AllActions => _allActions;

	public static void RegisterTrigger<T>(string displayName) where T : WorkflowTrigger, new()
		=> _allTriggers.Add(new WorkflowTriggerInfo(typeof(T), displayName, () => new T()));
	public static void RegisterAction<T>(string displayName) where T : WorkflowAction, new()
		=> _allActions.Add(new WorkflowActionInfo(typeof(T), displayName, () => new T()));

	private ILogger Logger { get; }

	public WorkflowService(ILogger<WorkflowService> logger)
	{
		Logger = logger;
	}

	public ObservableCollection<Workflow> UserWorkflows { get; } = new();
	public ObservableCollection<Workflow> SystemWorkflows { get; } = new();
	public ObservableCollection<Workflow> Workflows => UserWorkflows;

	public void LoadWorkflows()
	{
		UserWorkflows.Clear();
		foreach (var workflow in ConfigurationLoader.LoadWorkflows())
			UserWorkflows.Add(workflow);
	}

	public void SaveWorkflows()
		=> ConfigurationLoader.SaveWorkflows(UserWorkflows.ToArray());


	public void PublishEvent(WorkflowEvent e)
	{
		Logger.LogDebug("イベント {EventType}/{EventId} がトリガーされました", e.EventType, e.EventId);
		
		var triggeredUserWorkflows = UserWorkflows.Where(w => w.Enabled && (w.Trigger?.CheckTrigger(e) ?? false)).ToArray();
		// ユーザーワークフローの実行
		var userWorkflowTasks = triggeredUserWorkflows.Select(async w =>
		{
			try
			{
				Logger.LogDebug("ユーザーワークフロー {Name} がトリガーされました", w.Name);
				await w.Actions.PrepareAsync(e);
				await w.Actions.ExecuteAsync(e);
			}
			catch (ScriptRuntimeException ex)
			{
				// ユーザーが記述したテンプレートの問題のため、Sentry に送信しない
				Logger.LogWarning(ex, "ユーザーワークフロー {Name} のテンプレート実行中にエラーが発生しました", w.Name);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "ユーザーワークフロー {Name} の実行中に例外が発生しました", w.Name);
			}
		});

		var triggeredSystemWorkflows = SystemWorkflows.Where(w => w.Enabled && (w.Trigger?.CheckTrigger(e) ?? false)).ToArray();
		// システムワークフローの実行
		var systemWorkflowTasks = triggeredSystemWorkflows.Select(async w =>
		{
			try
			{
				Logger.LogDebug("システムワークフロー {Name} がトリガーされました", w.Name);
				await w.Actions.PrepareAsync(e);
				await w.Actions.ExecuteAsync(e);
			}
			catch (ScriptRuntimeException ex)
			{
				// ユーザーが記述したテンプレートの問題のため、Sentry に送信しない
				Logger.LogWarning(ex, "システムワークフロー {Name} のテンプレート実行中にエラーが発生しました", w.Name);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "システムワークフロー {Name} の実行中に例外が発生しました", w.Name);
			}
		});

		// ユーザーワークフローとシステムワークフローを並行実行
		Task.WhenAll(userWorkflowTasks.Concat(systemWorkflowTasks).ToArray()).ConfigureAwait(false);
	}
}
