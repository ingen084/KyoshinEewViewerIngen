#if WINDOWS
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using Splat;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using KyoshinEewViewer.Notification;
using static KyoshinEewViewer.Desktop.Notification.Windows.NativeMethods;

namespace KyoshinEewViewer.Desktop.Notification.Windows;

public class WindowsNotificationProvider : NotificationProvider
{
	public override bool TrayIconAvailable => IsInitialized;

	public bool IsInitialized { get; private set; }
	private bool ShutdownRequested { get; set; } = false;

	private Thread? TrayIconThread { get; set; }
	private TrayMenuItem[]? TrayMenuItems { get; set; }

	private readonly string _trayIconClassName = "kevi_tray_icon_" + Guid.NewGuid();
	private Notifyicondata _notifyIconData;
	private IntPtr _hWnd = IntPtr.Zero;
	private IntPtr _hMenu = IntPtr.Zero;
	private uint _uTaskbarRestart = 0;

	public WindowsNotificationProvider()
		// Toast をアプリ名/アイコン付きで表示し通知設定に登録するため AUMID を登録する
		=> WindowsToastIdentity.Register();

	public override void InitializeTrayIcon(TrayMenuItem[] menuItems)
	{
		TrayMenuItems = menuItems;
		TrayIconThread = new Thread(CreateAndHostTrayIcon);
		TrayIconThread.Start();
	}


	public override void Dispose()
	{
		// ポータブル配布のため AUMID 登録を残さず削除する
		WindowsToastIdentity.Unregister();

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

		if (IsInitialized)
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

		IsInitialized = true;

		while (GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0)
		{
			//Debug.WriteLine($"WndProc GET: {msg.message}");
			if (ShutdownRequested)
				break;
			if (msg.message == WmQuit)
				break;
			DispatchMessage(ref msg);
		}

		Debug.WriteLine("exit");
	}

	public override void SendNotice(NotificationRequest request)
	{
		try
		{
			// 通知音はアプリ側 (PlaySoundAction 等) が担うため OS 側は無音にする
			// 重要通知は画面に残り続けるよう Reminder シナリオにする
			var scenario = request.Urgency == NotificationUrgency.Critical ? " scenario=\"reminder\"" : "";
			var xmlText =
				$"<toast{scenario}>" +
					"<visual><binding template=\"ToastGeneric\">" +
						$"<text>{SecurityElement.Escape(request.Title)}</text>" +
						$"<text>{SecurityElement.Escape(request.Message)}</text>" +
					"</binding></visual>" +
					"<audio silent=\"true\" />" +
				"</toast>";

			var xml = new XmlDocument();
			xml.LoadXml(xmlText);

			// WindowsToastIdentity で登録した AUMID (CustomActivator) 宛てに表示する
			ToastNotificationManager.CreateToastNotifier(NotificationProvider.ApplicationId)
				.Show(new ToastNotification(xml));
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "トースト通知に失敗したためトレイバルーンにフォールバックします");
			SendBalloon(request);
		}
	}

	// トースト送信に失敗した場合のフォールバック (トレイアイコンのバルーン)
	private void SendBalloon(NotificationRequest request)
	{
		if (!IsInitialized)
			return;

		_notifyIconData.uFlags |= Nif.NifInfo;
		_notifyIconData.szInfoTitle = request.Title;
		_notifyIconData.szInfo = request.Message;
		Shell_NotifyIcon(NimModify, ref _notifyIconData);
	}

	private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
	{
		Debug.WriteLine($"WndProc: {uMsg}");
		if (ShutdownRequested)
			return IntPtr.Zero;

		switch (uMsg)
		{
			case WmCreate:
				_uTaskbarRestart = RegisterWindowMessage("TaskbarCreated");
				return IntPtr.Zero;
			case WmTrayCallbackMessage:
				return WndProcTrayCallback(hWnd, uMsg, wParam, lParam);
			case WmCommand:
				//Debug.WriteLine($"WMCommand: w{wParam} l{lParam}");
				Dispatcher.UIThread.Post(() => TrayMenuItems?.FirstOrDefault(i => i.Id == (uint)wParam)?.OnClicked());
				return IntPtr.Zero;
			default:
				if (uMsg == _uTaskbarRestart)
				{
					Shell_NotifyIcon(NimAdd, ref _notifyIconData);
					return IntPtr.Zero;
				}
				return DefWindowProc(hWnd, uMsg, wParam, lParam);
		}
	}
	private IntPtr WndProcTrayCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
	{
		switch (lParam.ToInt32())
		{
			case WmLbuttondblclk:
			case NinBalloonUserclick:
				Dispatcher.UIThread.Post(() => MessageBus.Current.SendMessage(new ShowMainWindowRequested()));
				return IntPtr.Zero;
			case WmRbuttonup:
				if (_hMenu == IntPtr.Zero)
					return IntPtr.Zero;
				GetCursorPos(out var p);
				SetForegroundWindow(hWnd);
				var pp = TrackPopupMenu(_hMenu, (uint)(TpmFlag.Leftalign | TpmFlag.Rightbutton | TpmFlag.Returncmd | TpmFlag.Nonotify), p.X, p.Y, 0, hWnd, IntPtr.Zero);
				SendMessage(hWnd, WmCommand, pp, IntPtr.Zero);
				return IntPtr.Zero;
			default:
				return DefWindowProc(hWnd, uMsg, wParam, lParam);
		}
	}
}
#endif
