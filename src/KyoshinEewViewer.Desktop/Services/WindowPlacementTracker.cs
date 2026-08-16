using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using Splat;
using System;
using System.Linq;
using System.Reactive.Linq;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Desktop.Services;

/// <summary>
/// ウィンドウの位置・サイズを追跡して設定に記録するクラス<br/>
/// モニタの切断など OS 起因のウィンドウ移動は記録せず、モニタ復帰時に元の位置へ戻す
/// </summary>
public sealed class WindowPlacementTracker : IDisposable
{
	/// <summary>
	/// モニタ構成変更後、この時間内の位置・サイズの変化を OS 起因とみなして記録しない
	/// </summary>
	private static readonly TimeSpan SuppressDuration = TimeSpan.FromSeconds(5);
	/// <summary>
	/// モニタ構成変更が連続して発生しなくなってから復元を試みるまでの時間
	/// </summary>
	private static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(2);

	private Window Window { get; }
	private Screens Screens { get; }
	private IWindowPlacementConfig Config { get; }
	private IDisposable EventSubscription { get; }
	private DispatcherTimer RestoreTimer { get; }

	/// <summary>
	/// OS 起因のウィンドウ移動を記録しない期限
	/// </summary>
	private DateTime SuppressUntil { get; set; } = DateTime.MinValue;
	/// <summary>
	/// OS 起因の移動によって実際の位置が意図した位置と異なっている可能性があるかどうか
	/// </summary>
	private bool HasOsMovedWindow { get; set; }

	private WindowState IntendedState { get; set; } = WindowState.Normal;
	private PixelPoint? IntendedPosition { get; set; }
	private Size? IntendedClientSize { get; set; }

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
				Observable.FromEventPattern<PixelPointEventArgs>(window, nameof(window.PositionChanged)).Select(_ => 0),
				Observable.FromEventPattern<EventArgs>(window, nameof(window.SizeChanged)).Select(_ => 0)
			)
				.Throttle(TimeSpan.FromMilliseconds(500)),
			UiScheduler.Instance)
			.Subscribe(_ => OnPlacementChanged());

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
		var point = new PixelPoint((int)location.X + 20, (int)location.Y + 20);
		return window.Screens.All.Any(s => s.Bounds.Contains(point));
	}

	/// <summary>
	/// 意図した配置を設定に書き込む<br/>
	/// OS 起因の移動が未解決の間は、最後にユーザー操作で確定した配置を保持したまま保存する
	/// </summary>
	public void Save()
	{
		if (!HasOsMovedWindow && DateTime.Now >= SuppressUntil)
			UpdateIntendedPlacement();
		Config.WindowState = Window.WindowState;
		if (IntendedPosition is { } position)
			Config.WindowLocation = new(position.X, position.Y);
		if (IntendedClientSize is { } size)
			Config.WindowSize = new(size.Width, size.Height);
	}

	private void OnOpened(object? sender, EventArgs e)
		=> UpdateIntendedPlacement();

	private void OnPlacementChanged()
	{
		if (DateTime.Now < SuppressUntil)
		{
			// モニタ構成変更直後の変化は OS によるウィンドウ移動とみなして記録しない
			HasOsMovedWindow = true;
			return;
		}
		HasOsMovedWindow = false;
		Save();
	}

	private void UpdateIntendedPlacement()
	{
		if (Window.WindowState == WindowState.Minimized)
			return;
		IntendedState = Window.WindowState;
		if (Window.WindowState == WindowState.FullScreen)
			return;
		IntendedPosition = Window.Position;
		if (Window.WindowState != WindowState.Maximized)
			IntendedClientSize = Window.ClientSize;
	}

	private void OnScreensChanged(object? sender, EventArgs e)
	{
		// OS によるウィンドウの移動をユーザーの操作と誤認しないようにする
		SuppressUntil = DateTime.Now + SuppressDuration;
		// 短時間に連続して発生するため、構成が安定してから復元を試みる
		RestoreTimer.Stop();
		RestoreTimer.Start();
	}

	/// <summary>
	/// 意図した位置のモニタが存在していれば、OS によって移動させられたウィンドウを元の位置に戻す
	/// </summary>
	private void RestorePlacement()
	{
		if (IntendedPosition is not { } intended || IntendedState == WindowState.Minimized)
			return;
		// 非表示中に WindowState を操作するとウィンドウが表示されてしまうため手を出さない
		if (Window.WindowState == WindowState.Minimized || !Window.IsVisible)
			return;

		// 意図した位置のモニタが存在しない場合はなにもしない
		var intendedCheckPoint = new PixelPoint(intended.X + 20, intended.Y + 20);
		var intendedScreen = Screens.All.FirstOrDefault(s => s.Bounds.Contains(intendedCheckPoint));
		if (intendedScreen == null)
			return;

		if (IntendedState == WindowState.Normal)
		{
			if (Window.WindowState == WindowState.Normal && Window.Position == intended)
				return;
			LogHost.Default.Info($"モニタ構成の変更で移動したウィンドウを元の位置に戻します: {intended}");
			Window.WindowState = WindowState.Normal;
			Window.Position = intended;
			ApplyIntendedClientSize();
		}
		else
		{
			// 最大化・フルスクリーン中は一度通常状態で目的のモニタに移動させてから状態を戻す
			var currentScreen = Screens.ScreenFromWindow(Window);
			if (currentScreen?.Bounds == intendedScreen.Bounds && Window.WindowState == IntendedState)
				return;
			LogHost.Default.Info($"モニタ構成の変更で移動したウィンドウを元のモニタに戻します: {intended} ({IntendedState})");
			Window.WindowState = WindowState.Normal;
			Window.Position = intended;
			ApplyIntendedClientSize();
			Window.WindowState = IntendedState;
		}
		HasOsMovedWindow = false;
		// 復元による位置変化を記録しないようにする
		var suppressUntil = DateTime.Now + TimeSpan.FromSeconds(1);
		if (SuppressUntil < suppressUntil)
			SuppressUntil = suppressUntil;
	}

	private void ApplyIntendedClientSize()
	{
		// Window の Width/Height はクライアント領域の論理サイズに相当する
		if (IntendedClientSize is not { } size)
			return;
		Window.Width = size.Width;
		Window.Height = size.Height;
	}

	public void Dispose()
	{
		EventSubscription.Dispose();
		RestoreTimer.Stop();
		Window.Opened -= OnOpened;
		Screens.Changed -= OnScreensChanged;
	}
}
