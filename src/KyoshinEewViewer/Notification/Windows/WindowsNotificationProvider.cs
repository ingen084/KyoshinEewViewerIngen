using Avalonia.Threading;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static KyoshinEewViewer.Notification.Windows.NativeMethods;

namespace KyoshinEewViewer.Notification.Windows;

public class WindowsNotificationProvider : NotificationProvider
{
	public override bool TrayIconAvailable => IsInitalized;

	public bool IsInitalized { get; private set; }
	private bool ShutdownRequested { get; set; } = false;

	private Thread? TrayIconThread { get; set; }
	private TrayMenuItem[]? TrayMenuItems { get; set; }

	private readonly string _trayIconClassName = "kevi_tray_icon_" + Guid.NewGuid();
	private Notifyicondata _notifyIconData;
	private IntPtr _hWnd = IntPtr.Zero;
	private IntPtr _hMenu = IntPtr.Zero;

	public override void InitalizeTrayIcon(TrayMenuItem[] menuItems)
	{
		TrayMenuItems = menuItems;
		TrayIconThread = new Thread(CreateAndHostTrayIcon);
		TrayIconThread.Start();
	}


	public override void Dispose()
	{
		ShutdownRequested = true;

		if (_hMenu != IntPtr.Zero)
			DestroyMenu(_hMenu);
		_hMenu = IntPtr.Zero;

		if (_hWnd != IntPtr.Zero)
		{
			SetTimer(_hWnd, new IntPtr(2), 1, IntPtr.Zero);
			DestroyWindow(_hWnd);
		}
		_hWnd = IntPtr.Zero;

		if (IsInitalized)
		{
			Shell_NotifyIcon(NimDelete, ref _notifyIconData);

			if (_notifyIconData.hIcon != IntPtr.Zero)
				DestroyIcon(_notifyIconData.hIcon);

			UnregisterClass(_trayIconClassName, GetModuleHandle(null));
		}
		GC.SuppressFinalize(this);
	}

	private void CreateAndHostTrayIcon()
	{
		var hInstance = GetModuleHandle(null);

		// ウィンドウクラス登録
		var wndClassEx = new Wndclassex
		{
			cbSize = Marshal.SizeOf<Wndclassex>(),
			lpfnWndProc = WndProc,
			hInstance = hInstance,
			lpszClassName = _trayIconClassName,
			style = (int)CsDblclks
		};
		var hCls = RegisterClassEx(ref wndClassEx);
		if (hCls == 0)
			throw new Exception("RegisterClassExに失敗: " + Marshal.GetLastWin32Error());

		_hWnd = CreateWindowEx(0, hCls, null, 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
		if (_hWnd == IntPtr.Zero)
			throw new Exception("CreateWindowExに失敗: " + Marshal.GetLastWin32Error());

		UpdateWindow(_hWnd);
		if (TrayMenuItems?.Any() ?? false)
		{
			_hMenu = CreatePopupMenu();
			foreach (var item in TrayMenuItems)
			{
				var menuItemInfo = new Menuiteminfo
				{
					cbSize = Marshal.SizeOf<Menuiteminfo>(),
					fMask = Miim.Id | Miim.Type | Miim.State | Miim.Data,
					wID = item.Id,
					dwTypeData = item.Text,
				};
				InsertMenuItem(_hMenu, item.Id, false, ref menuItemInfo);
			}
		}

		_notifyIconData = new Notifyicondata
		{
			cbSize = Marshal.SizeOf<Notifyicondata>(),
			hWnd = _hWnd,
			uID = 0,
			uCallbackMessage = WmTrayCallbackMessage,
			uFlags = Nif.NifIcon | Nif.NifTip | Nif.NifMessage,
			szTip = "KyoshinEewViewer for ingen",
			szInfo = "通知アイコンが有効です",
			szInfoTitle = "起動しました",
		};

		var hIcons = new[] { IntPtr.Zero };
		ExtractIconEx(Environment.GetCommandLineArgs()[0], 0, null, hIcons, 1);
		_notifyIconData.hIcon = hIcons[0];
		Shell_NotifyIcon(NimAdd, ref _notifyIconData);

		IsInitalized = true;

		while (GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0)
		{
			//Debug.WriteLine($"WndProc GET: {msg.message}");
			if (ShutdownRequested)
				break;
			if (msg.message == WmQuit)
				break;
			//TranslateMessage(ref msg);
			DispatchMessage(ref msg);
		}

		Debug.WriteLine("exit");
	}

	public override void SendNotice(string title, string message)
	{
		if (!IsInitalized)
			return;

		_notifyIconData.uFlags |= Nif.NifInfo;
		_notifyIconData.szInfoTitle = title;
		_notifyIconData.szInfo = message;
		Shell_NotifyIcon(NimModify, ref _notifyIconData);
	}

	private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
	{
		//Debug.WriteLine($"WndProc: {uMsg}");
		if (ShutdownRequested)
			return IntPtr.Zero;

		if (uMsg == WmTrayCallbackMessage)
			return WndProcTrayCallback(hWnd, uMsg, wParam, lParam);
		if (uMsg == WmCommand)
		{
			Debug.WriteLine($"WMCommand: w{wParam} l{lParam}");
			Dispatcher.UIThread.InvokeAsync(() => TrayMenuItems?.FirstOrDefault(i => i.Id == (uint)wParam)?.OnClicked());
			return IntPtr.Zero;
		}
		return DefWindowProc(hWnd, uMsg, wParam, lParam);
	}
	private IntPtr WndProcTrayCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
	{
		if (lParam.ToInt32() == WmLbuttondblclk)
		{
			Dispatcher.UIThread.InvokeAsync(() => MessageBus.Current.SendMessage(new ShowMainWindowRequested()));
			return IntPtr.Zero;
		}
		if (lParam.ToInt32() != WmRbuttonup)
			return DefWindowProc(hWnd, uMsg, wParam, lParam);
		if (_hMenu == IntPtr.Zero)
			return IntPtr.Zero;
		GetCursorPos(out var p);
		SetForegroundWindow(hWnd);
		var pp = TrackPopupMenu(_hMenu, (uint)(TpmFlag.Leftalign | TpmFlag.Rightbutton | TpmFlag.Returncmd | TpmFlag.Nonotify), p.X, p.Y, 0, hWnd, IntPtr.Zero);
		SendMessage(hWnd, WmCommand, pp, IntPtr.Zero);
		return IntPtr.Zero;
	}
}
