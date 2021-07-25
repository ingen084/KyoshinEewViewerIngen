using System;
using System.Linq;
using static KyoshinEewViewer.Notification.Windows.NativeMethods;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;

namespace KyoshinEewViewer.Notification.Windows
{
	public class WindowsNotificationProvider : NotificationProvider
	{
		public bool IsInitalized { get; private set; }
		private bool ShutdownRequested { get; set; } = false;

		private Thread? TrayIconThread { get; set; }
		private TrayMenuItem[]? TrayMenuItems { get; set; }

		private readonly string TrayIconClassName = "kevi_tray_icon_" + Guid.NewGuid();
		private NOTIFYICONDATA NotifyIconData;
		private IntPtr hWnd = IntPtr.Zero;
		private IntPtr hMenu = IntPtr.Zero;

		public override void InitalizeTrayIcon(TrayMenuItem[] menuItems)
		{
			TrayMenuItems = menuItems;
			TrayIconThread = new Thread(CreateAndHostTrayIcon);
			TrayIconThread.Start();
		}


		public override void Dispose()
		{
			ShutdownRequested = true;

			if (hMenu != IntPtr.Zero)
				DestroyMenu(hMenu);
			hMenu = IntPtr.Zero;

			if (hWnd != IntPtr.Zero)
			{
				SetTimer(hWnd, new IntPtr(2), 1, IntPtr.Zero);
				DestroyWindow(hWnd);
			}
			hWnd = IntPtr.Zero;

			if (IsInitalized)
			{
				Shell_NotifyIcon(NIM_DELETE, ref NotifyIconData);

				if (NotifyIconData.hIcon != IntPtr.Zero)
					DestroyIcon(NotifyIconData.hIcon);

				UnregisterClass(TrayIconClassName, GetModuleHandle(null));
			}
			GC.SuppressFinalize(this);
		}

		private void CreateAndHostTrayIcon()
		{
			var hInstance = GetModuleHandle(null);

			// ウィンドウクラス登録
			var wndClassEx = new WNDCLASSEX
			{
				cbSize = Marshal.SizeOf<WNDCLASSEX>(),
				lpfnWndProc = WndProc,
				hInstance = hInstance,
				lpszClassName = TrayIconClassName,
				style = (int)CS_DBLCLKS
			};
			var hCls = RegisterClassEx(ref wndClassEx);
			if (hCls == 0)
				throw new Exception("RegisterClassExに失敗: " + Marshal.GetLastWin32Error());

			hWnd = CreateWindowEx(0, hCls, null, 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
			if (hWnd == IntPtr.Zero)
				throw new Exception("CreateWindowExに失敗: " + Marshal.GetLastWin32Error());

			UpdateWindow(hWnd);
			if (TrayMenuItems?.Any() ?? false)
			{
				hMenu = CreatePopupMenu();
				foreach (var item in TrayMenuItems)
				{
					var menuItemInfo = new MENUITEMINFO
					{
						cbSize = Marshal.SizeOf<MENUITEMINFO>(),
						fMask = MIIM.ID | MIIM.TYPE | MIIM.STATE | MIIM.DATA,
						wID = item.Id,
						dwTypeData = item.Text,
					};
					InsertMenuItem(hMenu, item.Id, false, ref menuItemInfo);
				}
			}

			NotifyIconData = new NOTIFYICONDATA
			{
				cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
				hWnd = hWnd,
				uID = 0,
				uCallbackMessage = WM_TRAY_CALLBACK_MESSAGE,
				uFlags = NIF.NIF_ICON | NIF.NIF_TIP | NIF.NIF_MESSAGE,
				szTip = "KyoshinEewViewer for ingen",
				szInfo = "通知アイコンが有効です",
				szInfoTitle = "起動しました",
			};

			var hIcons = new[] { IntPtr.Zero };
			ExtractIconEx(Environment.GetCommandLineArgs()[0], 0, null, hIcons, 1);
			NotifyIconData.hIcon = hIcons[0];
			Shell_NotifyIcon(NIM_ADD, ref NotifyIconData);

			IsInitalized = true;

			while (GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0)
			{
				//Debug.WriteLine($"WndProc GET: {msg.message}");
				if (ShutdownRequested)
					break;
				if (msg.message == WM_QUIT)
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

			NotifyIconData.uFlags |= NIF.NIF_INFO;
			NotifyIconData.szInfoTitle = title;
			NotifyIconData.szInfo = message;
			Shell_NotifyIcon(NIM_MODIFY, ref NotifyIconData);
		}

		private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
		{
			//Debug.WriteLine($"WndProc: {uMsg}");
			if (ShutdownRequested)
				return IntPtr.Zero;

			if (uMsg == WM_TRAY_CALLBACK_MESSAGE)
				return WndProcTrayCallback(hWnd, uMsg, wParam, lParam);
			if (uMsg == WM_COMMAND)
			{
				Debug.WriteLine($"WMCommand: w{wParam} l{lParam}");
				Dispatcher.UIThread.InvokeAsync(() => TrayMenuItems?.FirstOrDefault(i => i.Id == (uint)wParam)?.OnClicked());
				return IntPtr.Zero;
			}
			return DefWindowProc(hWnd, uMsg, wParam, lParam);
		}
		private IntPtr WndProcTrayCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
		{
			if (lParam.ToInt32() == WM_LBUTTONDBLCLK)
			{
				Dispatcher.UIThread.InvokeAsync(() => MessageBus.Current.SendMessage(new ShowMainWindowRequested()));
				return IntPtr.Zero;
			}
			if (lParam.ToInt32() != WM_RBUTTONUP)
				return DefWindowProc(hWnd, uMsg, wParam, lParam);
			if (hMenu == IntPtr.Zero)
				return IntPtr.Zero;
			GetCursorPos(out var p);
			SetForegroundWindow(hWnd);
			var pp = TrackPopupMenu(hMenu, (uint)(TpmFlag.LEFTALIGN | TpmFlag.RIGHTBUTTON | TpmFlag.RETURNCMD | TpmFlag.NONOTIFY), p.X, p.Y, 0, hWnd, IntPtr.Zero);
			SendMessage(hWnd, WM_COMMAND, pp, IntPtr.Zero);
			return IntPtr.Zero;
		}
	}
}
