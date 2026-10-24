using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Desktop.Services;

/// <summary>
/// ウィンドウの実際の配置
/// </summary>
/// <param name="Screen">ウィンドウが表示されているモニタ。特定できない場合は null</param>
internal readonly record struct WindowPlacement(WindowState State, PixelPoint Position, Size ClientSize, WindowPlacementScreen? Screen);

/// <summary>
/// ユーザーが意図したウィンドウの配置と、OS 起因の移動を見分けるための状態<br/>
/// ウィンドウやモニタには触れず、記録・保存・復元の判断だけを受け持つ
/// </summary>
internal sealed class WindowPlacementState
{
	/// <summary>
	/// モニタ構成変更後、この時間内の位置・サイズの変化を OS 起因とみなして記録しない
	/// </summary>
	private static readonly TimeSpan SuppressDuration = TimeSpan.FromSeconds(5);
	/// <summary>
	/// 復元によって生じた位置・サイズの変化を記録しない時間
	/// </summary>
	private static readonly TimeSpan RestoreSettleDuration = TimeSpan.FromSeconds(1);
	/// <summary>
	/// 意図したサイズと同じとみなすクライアント領域の誤差 (論理ピクセル)
	/// </summary>
	private const double ClientSizeTolerance = 1;

	/// <summary>
	/// OS 起因のウィンドウ移動を記録しない期限
	/// </summary>
	private DateTime SuppressUntil { get; set; } = DateTime.MinValue;
	/// <summary>
	/// OS 起因の移動によって実際の位置が意図した位置と異なっている可能性があるかどうか
	/// </summary>
	public bool HasOsMovedWindow { get; private set; }

	public WindowState IntendedState { get; private set; } = WindowState.Normal;
	public Size? IntendedClientSize { get; private set; }
	private PixelPoint? IntendedPosition { get; set; }
	private WindowPlacementScreen? IntendedScreen { get; set; }

	/// <summary>
	/// 現在の配置を意図した配置として記録してよいかどうか
	/// </summary>
	public bool CanRecord(DateTime now)
		=> !HasOsMovedWindow && now >= SuppressUntil;

	/// <summary>
	/// 現在の配置を意図した配置として記録する
	/// </summary>
	public void Record(WindowPlacement current)
	{
		if (current.State == WindowState.Minimized)
			return;
		IntendedState = current.State;
		if (current.State == WindowState.FullScreen)
		{
			// 全画面を抜けたときに戻れるよう、同じモニタ上の通常時の位置を残す
			if (IntendedPosition is { } position && IntendedScreen is { } screen && current.Screen is { } fullScreen && screen.IsSameScreen(fullScreen))
			{
				IntendedPosition = screen.TranslatePosition(position, fullScreen);
				IntendedScreen = fullScreen;
				return;
			}
			// 全画面で起動した直後は、設定に保存済みの位置をそのまま残す
			if (IntendedPosition == null)
			{
				IntendedScreen = current.Screen;
				return;
			}
		}
		IntendedPosition = current.Position;
		IntendedScreen = current.Screen;
		if (current.State == WindowState.Normal)
			IntendedClientSize = current.ClientSize;
	}

	/// <summary>
	/// モニタ構成が変わったことを記録する
	/// </summary>
	public void OnScreensChanged(DateTime now)
	{
		// OS によるウィンドウの移動をユーザーの操作と誤認しないようにする
		SuppressUntil = now + SuppressDuration;
		// 移動通知がなくても、復元までの定期保存で元の配置を上書きさせない
		HasOsMovedWindow = true;
	}

	/// <summary>
	/// ウィンドウの位置・サイズが変化したことを記録する
	/// </summary>
	/// <param name="changedAt">変化が発生した時刻</param>
	/// <returns>ユーザーの操作による変化とみなせる場合は true</returns>
	public bool OnPlacementChanged(DateTime changedAt)
	{
		// モニタ構成変更直後の変化は OS によるウィンドウ移動とみなして記録しない
		HasOsMovedWindow = changedAt < SuppressUntil;
		return !HasOsMovedWindow;
	}

	/// <summary>
	/// 設定に保存するウィンドウの状態
	/// </summary>
	public WindowState GetSavedState(WindowState current)
		=> current == WindowState.Minimized ? WindowState.Minimized : IntendedState;

	/// <summary>
	/// 設定に保存するウィンドウの位置。保存すべき位置がない場合は null
	/// </summary>
	public PixelPoint? GetSavedPosition(IEnumerable<WindowPlacementScreen> screens)
	{
		if (IntendedPosition is not { } position)
			return null;
		// OS 起因の移動が未解決の間にモニタの原点が変わっていれば、現在の座標系で保存する
		if (HasOsMovedWindow && IntendedScreen?.FindCurrentScreen(screens) is { } screen)
			return IntendedScreen.TranslatePosition(position, screen);
		return position;
	}

	/// <summary>
	/// モニタ構成が安定した後に、意図した配置と実際の配置を突き合わせる
	/// </summary>
	/// <returns>意図した配置に戻すための移動先。動かす必要がない場合や戻せない場合は null</returns>
	public PixelPoint? Reconcile(WindowPlacement current, IEnumerable<WindowPlacementScreen> screens, DateTime now)
	{
		if (current.State == WindowState.Minimized)
			return null;
		// 意図した位置のモニタが存在しない場合はなにもしない
		if (IntendedScreen is not { } saved || saved.FindCurrentScreen(screens) is not { } screen)
			return null;

		// 全画面で起動して位置が未記録の場合は、モニタの左上へ移動してから全画面にさせる
		var position = saved.TranslatePosition(IntendedPosition ?? saved.Bounds.Position, screen);
		if (IntendedPosition != null)
			IntendedPosition = position;
		IntendedScreen = screen;
		HasOsMovedWindow = false;

		var isIntended = IntendedState == WindowState.Normal
			? current.State == WindowState.Normal && current.Position == position && IsIntendedClientSize(current.ClientSize)
			: current.State == IntendedState && current.Screen?.IsSameScreen(screen) == true;
		// 意図した配置にあると確かめた後の変化はユーザーの操作として扱う。戻した場合は、戻したことによる変化だけを記録しない
		SuppressUntil = isIntended ? now : now + RestoreSettleDuration;
		return isIntended ? null : position;
	}

	private bool IsIntendedClientSize(Size size)
		=> IntendedClientSize is not { } intended
			|| (Math.Abs(size.Width - intended.Width) < ClientSizeTolerance && Math.Abs(size.Height - intended.Height) < ClientSizeTolerance);
}
