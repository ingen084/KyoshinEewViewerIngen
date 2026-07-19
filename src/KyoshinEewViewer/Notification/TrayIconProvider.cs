using System;

namespace KyoshinEewViewer.Notification;

/// <summary>
/// タスクトレイ (通知領域) のアイコンを提供する。
/// Avalonia の TrayIcon を用いるためプラットフォーム非依存で、実装は起動アプリ (Desktop) が DI に登録する
/// </summary>
public abstract class TrayIconProvider : IDisposable
{
	/// <summary>
	/// ウィンドウをトレイに格納しても復帰可能か。
	/// トレイが確実に表示される環境 (Windows/macOS、StatusNotifierItem に対応した Linux) でのみ true。
	/// false の場合はウィンドウを隠すと復帰手段が失われるため、格納機能を無効にする
	/// </summary>
	public abstract bool CanHideToTray { get; }

	/// <summary>トレイアイコンとコンテキストメニューを表示する</summary>
	public abstract void Show(TrayMenuItem[] menuItems);

	public abstract void Dispose();
}
