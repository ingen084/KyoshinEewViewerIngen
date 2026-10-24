using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using KyoshinEewViewer.Core;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Desktop.Services;

/// <summary>
/// ウィンドウの位置・サイズを追跡して設定に記録するクラス<br/>
/// モニタの切断など OS 起因のウィンドウ移動は記録せず、モニタ復帰時に元の位置へ戻す
/// </summary>
public sealed class WindowPlacementTracker : IDisposable
{
	/// <summary>
	/// モニタ構成変更が連続して発生しなくなってから復元を試みるまでの時間
	/// </summary>
	private static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(2);

	private Window Window { get; }
	private Screens Screens { get; }
	private IWindowPlacementConfig Config { get; }
	private IDisposable EventSubscription { get; }
	private DispatcherTimer RestoreTimer { get; }
	private WindowPlacementState State { get; } = new();

	public WindowPlacementTracker(Window window, IWindowPlacementConfig config)
	{
		Window = window;
		Screens = window.Screens;
		Config = config;

		RestoreTimer = new DispatcherTimer { Interval = RestoreDelay };
		RestoreTimer.Tick += (s, e) =>
		{
			RestoreTimer.Stop();
			RestorePlacement();
		};

		// ObserveOn を拡張メソッド形式で書くと、windows TFM の System.Reactive が持つ
		// WPF/WinForms 向けオーバーロードの解決に WindowsDesktop 参照が要求されるため、
		// Observable クラスの静的メソッド形式で呼び出す (WindowsDesktop 同梱回避とセット)
		EventSubscription = Observable.ObserveOn(
			Observable.Merge(
				Observable.FromEventPattern<PixelPointEventArgs>(window, nameof(window.PositionChanged)).Select(_ => DateTime.UtcNow),
				Observable.FromEventPattern<EventArgs>(window, nameof(window.SizeChanged)).Select(_ => DateTime.UtcNow)
			)
				.Throttle(TimeSpan.FromMilliseconds(500)),
			UiScheduler.Instance)
			.Subscribe(OnPlacementChanged);

		Window.Opened += OnOpened;
		Screens.Changed += OnScreensChanged;
	}

	/// <summary>
	/// 保存されたウィンドウ位置が現在のモニタ上に存在する有効な位置かどうかを判定する
	/// </summary>
	public static bool IsValidLocation(Window window, KyoshinEewViewerConfiguration.Point2D location)
	{
		// -32000 は最小化されたウィンドウを表す座標
		if (location.X == -32000 || location.Y == -32000)
			return false;
		// タイトルバー付近がいずれかのモニタに含まれているか
		var position = new PixelPoint((int)location.X, (int)location.Y);
		return window.Screens.All.Any(s => WindowPlacementScreen.ContainsTitleBar(s.Bounds, position));
	}

	/// <summary>
	/// 意図した配置を設定に書き込む<br/>
	/// OS 起因の移動が未解決の間は、最後にユーザー操作で確定した配置を保持したまま保存する
	/// </summary>
	public void Save()
	{
		if (State.CanRecord(DateTime.UtcNow))
			State.Record(GetCurrentPlacement());
		Config.WindowState = State.GetSavedState(Window.WindowState);
		if (State.GetSavedPosition(CaptureScreens()) is { } position)
			Config.WindowLocation = new(position.X, position.Y);
		if (State.IntendedClientSize is { } size)
			Config.WindowSize = new(size.Width, size.Height);
	}

	private void OnOpened(object? sender, EventArgs e)
		=> State.Record(GetCurrentPlacement());

	private void OnPlacementChanged(DateTime changedAt)
	{
		// Throttle の待機中に抑制期間を過ぎても、発生時刻が期間内なら保存しない。
		if (State.OnPlacementChanged(changedAt))
			Save();
	}

	private void OnScreensChanged(object? sender, EventArgs e)
	{
		State.OnScreensChanged(DateTime.UtcNow);
		// 短時間に連続して発生するため、構成が安定してから復元を試みる
		RestoreTimer.Stop();
		RestoreTimer.Start();
	}

	/// <summary>
	/// 意図した位置のモニタが存在していれば、OS によって移動させられたウィンドウを元の位置に戻す
	/// </summary>
	private void RestorePlacement()
	{
		// 非表示中に WindowState を操作するとウィンドウが表示されてしまうため手を出さない
		if (!Window.IsVisible)
			return;
		if (State.Reconcile(GetCurrentPlacement(), CaptureScreens(), DateTime.UtcNow) is not { } position)
			return;

		AppLog.Default.LogInformation("モニタ構成の変更で移動したウィンドウを元の配置に戻します: {Intended} ({IntendedState})", position, State.IntendedState);
		// 最大化・フルスクリーン中は一度通常状態で目的のモニタに移動させてから状態を戻す
		Window.WindowState = WindowState.Normal;
		Window.Position = position;
		ApplyIntendedClientSize();
		if (State.IntendedState != WindowState.Normal)
			Window.WindowState = State.IntendedState;
	}

	private void ApplyIntendedClientSize()
	{
		// Window の Width/Height はクライアント領域の論理サイズに相当する
		if (State.IntendedClientSize is not { } size)
			return;
		Window.Width = size.Width;
		Window.Height = size.Height;
	}

	private WindowPlacement GetCurrentPlacement()
	{
		// ウィンドウの大部分が表示されているモニタを優先し、取れない場合はタイトルバー付近のモニタを使う
		// 閉じた後のウィンドウでは ScreenFromWindow が例外を投げるため呼ばない
		var screen = (Window.PlatformImpl != null ? Screens.ScreenFromWindow(Window) : null)
			?? Screens.All.FirstOrDefault(s => WindowPlacementScreen.ContainsTitleBar(s.Bounds, Window.Position));
		return new(Window.WindowState, Window.Position, Window.ClientSize, screen == null ? null : CaptureScreen(screen));
	}

	/// <summary>
	/// 現在のモニタ一覧。必要になるまで識別子を取得しない
	/// </summary>
	private IEnumerable<WindowPlacementScreen> CaptureScreens()
		=> Screens.All.Select(CaptureScreen);

	private static WindowPlacementScreen CaptureScreen(Screen screen)
	{
		var handle = screen.TryGetPlatformHandle();
		string? id = null;
		// HMONITOR は構成変更のたびに振り直され、ディスプレイ番号や製品名では同型のモニタを区別できないため、デバイスパスを使う。
		// 取得できない場合は製品名などで代用せず、識別子のないモニタとして扱う。
		if (OperatingSystem.IsWindows() && handle?.HandleDescriptor == "HMonitor")
			id = GetMonitorDevicePath(handle.Handle);
		else if (handle != null)
			id = $"{handle.HandleDescriptor}:{handle.Handle}";
		return new(id, screen.Bounds, screen.WorkingArea);
	}

	private static string? GetMonitorDevicePath(IntPtr monitor)
	{
		var info = new NativeMethods.MonitorInfoEx { Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
		if (!NativeMethods.GetMonitorInfo(monitor, ref info))
			return null;
		var device = new NativeMethods.DisplayDevice { Size = Marshal.SizeOf<NativeMethods.DisplayDevice>() };
		if (!NativeMethods.EnumDisplayDevices(info.DeviceName, 0, ref device, NativeMethods.EddGetDeviceInterfaceName))
			return null;
		return string.IsNullOrEmpty(device.DeviceId) ? null : device.DeviceId;
	}

	public void Dispose()
	{
		EventSubscription.Dispose();
		RestoreTimer.Stop();
		Window.Opened -= OnOpened;
		Screens.Changed -= OnScreensChanged;
	}
}
