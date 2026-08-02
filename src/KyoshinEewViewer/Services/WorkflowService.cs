using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using Scriban.Syntax;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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

	public WorkflowService(ILogManager logManager)
	{
		SplatRegistrations.RegisterLazySingleton<WorkflowService>();

		Logger = logManager.GetLogger<WorkflowService>();
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
		Logger.LogDebug($"イベント {e.EventType}/{e.EventId} がトリガーされました");
		
		var triggeredUserWorkflows = UserWorkflows.Where(w => w.Enabled && (w.Trigger?.CheckTrigger(e) ?? false)).ToArray();
		// ユーザーワークフローの実行
		var userWorkflowTasks = triggeredUserWorkflows.Select(async w =>
		{
			try
			{
				Logger.LogDebug($"ユーザーワークフロー {w.Name} がトリガーされました");
				await w.Actions.PrepareAsync(e);
				await w.Actions.ExecuteAsync(e);
			}
			catch (ScriptRuntimeException ex)
			{
				// ユーザーが記述したテンプレートの問題のため、Sentry に送信しない
				Logger.LogWarning(ex, $"ユーザーワークフロー {w.Name} のテンプレート実行中にエラーが発生しました");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"ユーザーワークフロー {w.Name} の実行中に例外が発生しました");
			}
		});

		var triggeredSystemWorkflows = SystemWorkflows.Where(w => w.Enabled && (w.Trigger?.CheckTrigger(e) ?? false)).ToArray();
		// システムワークフローの実行
		var systemWorkflowTasks = triggeredSystemWorkflows.Select(async w =>
		{
			try
			{
				Logger.LogDebug($"システムワークフロー {w.Name} がトリガーされました");
				await w.Actions.PrepareAsync(e);
				await w.Actions.ExecuteAsync(e);
			}
			catch (ScriptRuntimeException ex)
			{
				// ユーザーが記述したテンプレートの問題のため、Sentry に送信しない
				Logger.LogWarning(ex, $"システムワークフロー {w.Name} のテンプレート実行中にエラーが発生しました");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"システムワークフロー {w.Name} の実行中に例外が発生しました");
			}
		});

		// ユーザーワークフローとシステムワークフローを並行実行
		Task.WhenAll(userWorkflowTasks.Concat(systemWorkflowTasks).ToArray()).ConfigureAwait(false);
	}
}
