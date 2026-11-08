using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Desktop.Services.InterProcess;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.Notification;

/// <summary>
/// 通知のクリックを、その通知を出したインスタンスで処理する。
/// <para>
/// Windows はクリックを、同じアプリとして登録しているインスタンスのどれか1つにしか渡さない。
/// そのため通知に送り元のプロセス ID を埋め込み、受け取ったインスタンスが送り元へ転送する。
/// </para>
/// </summary>
public static class NotificationActivation
{
	private const string ProcessIdKey = "pid";

	/// <summary>
	/// 通知に埋め込む引数を作る
	/// </summary>
	public static string CreateArguments() => $"{ProcessIdKey}={Environment.ProcessId}";

	/// <summary>
	/// クリックされた通知を、送り元のインスタンスへ届ける。送り元が自分か、送り元に届かなければ自分で処理する
	/// </summary>
	public static void Dispatch(string arguments)
	{
		if (TryGetProcessId(arguments) is not { } processId || processId == Environment.ProcessId)
		{
			Handle(arguments);
			return;
		}
		// 前面に出る権限はクリックを受け取ったプロセスにだけ与えられるので、転送より前に渡しておく
		if (OperatingSystem.IsWindows())
			NativeMethods.AllowSetForegroundWindow(processId);
		_ = ForwardAsync(processId, arguments);
	}

	/// <summary>
	/// このインスタンスで通知のクリックを処理する
	/// </summary>
	public static void Handle(string arguments)
		=> StrongReferenceMessenger.Default.Send(new ShowMainWindowRequested());

	private static async Task ForwardAsync(int processId, string arguments)
	{
		try
		{
			await InterProcessMessenger.SendAsync(
				InterProcessMessenger.GetEndpointName(processId),
				InterProcessMessages.NotificationActivated,
				new NotificationActivatedRequest(arguments));
		}
		catch (InterProcessException ex)
		{
			AppLog.Default.LogWarning(ex, "通知を出したインスタンスへクリックを転送できなかったため、このインスタンスで表示します: {ProcessId}", processId);
			Handle(arguments);
		}
	}

	private static int? TryGetProcessId(string arguments)
	{
		foreach (var pair in arguments.Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var separator = pair.IndexOf('=');
			if (separator > 0 && pair[..separator] == ProcessIdKey && int.TryParse(pair[(separator + 1)..], out var processId))
				return processId;
		}
		return null;
	}
}
