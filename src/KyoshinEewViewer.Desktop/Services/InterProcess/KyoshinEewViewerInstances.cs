using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.Services.InterProcess;

/// <summary>
/// 同じマシンで動いている KEVi のインスタンスを探し、インスタンス間通信でやりとりする
/// </summary>
public static class KyoshinEewViewerInstances
{
	/// <summary>配布版とデバッグ実行時の実行ファイル名</summary>
	private static readonly string[] ProcessNames = ["KyoshinEewViewer", "KyoshinEewViewer.Desktop"];

	/// <summary>
	/// 自分以外に動いている KEVi のプロセス ID を返す
	/// </summary>
	public static List<int> GetOtherProcessIds()
	{
		var ids = new List<int>();
		foreach (var name in ProcessNames)
		{
			foreach (var process in Process.GetProcessesByName(name))
			{
				using (process)
				{
					if (process.Id != Environment.ProcessId)
						ids.Add(process.Id);
				}
			}
		}
		return ids;
	}

	/// <summary>
	/// このインスタンスの接続先で待ち受けを開始する
	/// </summary>
	public static InterProcessMessenger StartMessenger()
	{
		var messenger = new InterProcessMessenger(
			InterProcessMessenger.GetEndpointName(Environment.ProcessId),
			AppLog.Create<InterProcessMessenger>());

		messenger.Register(InterProcessMessages.GetInstanceInfo, (_, _) =>
			Task.FromResult(new InstanceInfo(Environment.ProcessId, StartupOptions.Current?.StandaloneSeriesName, Utils.Version)));
		messenger.Register(InterProcessMessages.ShowMainWindow, (_, _) =>
		{
			StrongReferenceMessenger.Default.Send(new ShowMainWindowRequested());
			return Task.FromResult(InterProcessEmpty.Instance);
		});

		messenger.Start();
		return messenger;
	}

	/// <summary>
	/// スタンドアロンでない既存のインスタンスを探し、そのメインウィンドウを表示させる
	/// </summary>
	/// <returns>表示させられたか</returns>
	public static async Task<bool> TryShowExistingMainWindowAsync(ILogger logger)
	{
		foreach (var processId in GetOtherProcessIds())
		{
			var endpoint = InterProcessMessenger.GetEndpointName(processId);
			try
			{
				var info = await InterProcessMessenger.SendAsync(endpoint, InterProcessMessages.GetInstanceInfo, InterProcessEmpty.Instance);
				if (info.StandaloneSeriesName is not null)
					continue;
				await InterProcessMessenger.SendAsync(endpoint, InterProcessMessages.ShowMainWindow, InterProcessEmpty.Instance);
				logger.LogInformation("既存のインスタンスにメインウィンドウを表示させました: {ProcessId}", processId);
				return true;
			}
			catch (InterProcessException ex)
			{
				logger.LogWarning(ex, "既存のインスタンスと通信できませんでした: {ProcessId}", processId);
			}
		}
		return false;
	}
}
