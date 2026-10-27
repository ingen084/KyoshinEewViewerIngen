using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace KyoshinEewViewer;

/// <summary>
/// プロセスの資源の使用量を記録し、Sentry に送るイベントへ添付する<br/>
/// メモリ不足で落ちた際に、このプロセスが資源を使い切ったのか、システム全体で不足したのかを判断するために使う
/// </summary>
internal static class ProcessResourceRecorder
{
	/// <summary>
	/// 推移を記録する間隔
	/// </summary>
	private static readonly TimeSpan RecordInterval = TimeSpan.FromHours(1);
	/// <summary>
	/// 保持する推移の数 (直近1日分)
	/// </summary>
	internal const int MaxRecords = 24;

	private static readonly Queue<Dictionary<string, object?>> Records = new();
	private static Timer? _timer;

	/// <summary>
	/// 推移の記録を開始する
	/// </summary>
	public static void Start()
		=> _timer ??= new Timer(_ => Record(), null, RecordInterval, RecordInterval);

	/// <summary>
	/// 現在の使用量を推移に記録する
	/// </summary>
	internal static void Record()
	{
		var sample = Sample(includeThreads: true);
		lock (Records)
		{
			Records.Enqueue(sample);
			while (Records.Count > MaxRecords)
				Records.Dequeue();
		}
	}

	/// <summary>
	/// 記録済みの推移と現在の使用量をイベントに添付する
	/// </summary>
	public static SentryEvent Attach(SentryEvent sentryEvent)
	{
		// メモリ不足の最中は新たな取得に失敗しうるため、記録済みの推移を先に添付する
		TryRun(() =>
		{
			lock (Records)
				if (Records.Count > 0)
					sentryEvent.Contexts["process_resources_history"] = new Dictionary<string, object?> { ["samples"] = Records.ToArray() };
		});
		TryRun(() => sentryEvent.Contexts["process_resources"] = Sample(includeThreads: false));
		return sentryEvent;
	}

	/// <summary>
	/// 現在の使用量を取得する<br/>
	/// 項目ごとに取得し、取得できなかった項目は含めない
	/// </summary>
	/// <param name="includeThreads">スレッド数を含めるか (Windows ではシステム全体の情報を取得するため、メモリ不足の最中には取得しない)</param>
	private static Dictionary<string, object?> Sample(bool includeThreads)
	{
		var sample = new Dictionary<string, object?> { ["time"] = DateTimeOffset.Now.ToString("o") };

		if (OperatingSystem.IsWindows())
		{
			// メモリを確保しない、このプロセスだけを対象にした API で取得する
			var process = GetCurrentProcess();
			TryRun(() =>
			{
				var counters = new ProcessMemoryCountersEx { Size = (uint)Marshal.SizeOf<ProcessMemoryCountersEx>() };
				if (!K32GetProcessMemoryInfo(process, ref counters, counters.Size))
					return;
				sample["private_bytes"] = (long)counters.PrivateUsage;
				sample["working_set_bytes"] = (long)counters.WorkingSetSize;
			});
			TryRun(() =>
			{
				if (GetProcessHandleCount(process, out var handles))
					sample["handles"] = (long)handles;
			});
			TryRun(() =>
			{
				sample["gdi_objects"] = (long)GetGuiResources(process, GuiResourceGdiObjects);
				sample["user_objects"] = (long)GetGuiResources(process, GuiResourceUserObjects);
			});
			// システム全体のコミット (物理メモリ + ページファイル) の上限と空き
			TryRun(() =>
			{
				var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
				if (!GlobalMemoryStatusEx(ref status))
					return;
				sample["commit_limit_bytes"] = (long)status.TotalPageFile;
				sample["commit_available_bytes"] = (long)status.AvailPageFile;
			});
		}
		else
		{
			TryRun(() =>
			{
				using var process = Process.GetCurrentProcess();
				sample["private_bytes"] = process.PrivateMemorySize64;
				sample["working_set_bytes"] = process.WorkingSet64;
				sample["handles"] = (long)process.HandleCount;
			});
		}

		if (includeThreads)
			TryRun(() =>
			{
				using var process = Process.GetCurrentProcess();
				sample["threads"] = (long)process.Threads.Count;
			});
		return sample;
	}

	private static void TryRun(Action action)
	{
		try
		{
			action();
		}
		catch
		{
			// 取得に失敗しても、ほかの項目の取得やイベントの送信は妨げない
		}
	}

	private const uint GuiResourceGdiObjects = 0;
	private const uint GuiResourceUserObjects = 1;

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool K32GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCountersEx counters, uint size);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetProcessHandleCount(IntPtr process, out uint handleCount);

	[DllImport("user32.dll")]
	private static extern uint GetGuiResources(IntPtr process, uint flags);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessMemoryCountersEx
	{
		public uint Size;
		public uint PageFaultCount;
		public nuint PeakWorkingSetSize;
		public nuint WorkingSetSize;
		public nuint QuotaPeakPagedPoolUsage;
		public nuint QuotaPagedPoolUsage;
		public nuint QuotaPeakNonPagedPoolUsage;
		public nuint QuotaNonPagedPoolUsage;
		public nuint PagefileUsage;
		public nuint PeakPagefileUsage;
		public nuint PrivateUsage;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MemoryStatusEx
	{
		public uint Length;
		public uint MemoryLoad;
		public ulong TotalPhys;
		public ulong AvailPhys;
		public ulong TotalPageFile;
		public ulong AvailPageFile;
		public ulong TotalVirtual;
		public ulong AvailVirtual;
		public ulong AvailExtendedVirtual;
	}
}
