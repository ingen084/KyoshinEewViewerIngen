using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Notification.Windows
{
	internal static class NativeMethods
	{
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool DestroyMenu(IntPtr hMenu);
		[DllImport("user32.dll")]
		public static extern IntPtr CreatePopupMenu();
		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "InsertMenuItemW")]
		public static extern bool InsertMenuItem(IntPtr hMenu, uint uItem, bool fByPosition, [In] ref MENUITEMINFO lpmii);
		[DllImport("user32.dll")]
		public static extern IntPtr TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int reserved, IntPtr hwnd, IntPtr prcRect);
		[Flags]
		public enum TpmFlag
		{
			LEFTALIGN = 0x0000,
			RIGHTBUTTON = 0x0002,
			NONOTIFY = 0x0080,
			RETURNCMD = 0x0100,
		}
		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMenuItemInfoW")]
		public static extern bool GetMenuItemInfo(IntPtr hMenu, uint uItem, bool fByPosition, [In, Out] ref MENUITEMINFO lpmii);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct MENUITEMINFO
		{
			public int cbSize;
			public MIIM fMask;
			public uint fType;
			public uint fState;
			public uint wID;
			public IntPtr hSubMenu;
			public IntPtr hbmpChecked;
			public IntPtr hbmpUnchecked;
			public IntPtr dwItemData;
			public string dwTypeData;
			public uint cch; // length of dwTypeData
			public IntPtr hbmpItem;
		}
		[Flags]
		public enum MIIM
		{
			BITMAP = 0x00000080,
			CHECKMARKS = 0x00000008,
			DATA = 0x00000020,
			FTYPE = 0x00000100,
			ID = 0x00000002,
			STATE = 0x00000001,
			STRING = 0x00000040,
			SUBMENU = 0x00000004,
			TYPE = 0x00000010
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
		public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
		   IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

		[DllImport("user32.dll")]
		public static extern bool DestroyIcon(IntPtr hIcon);

		public const int WM_QUIT = 0x0012;
		public const int WM_USER = 0x0400;
		public const int WM_TRAY_CALLBACK_MESSAGE = WM_USER + 1;
		public const int WM_COMMAND = 0x0111;

		public const int WM_LBUTTONUP = 0x0202;
		public const int WM_LBUTTONDBLCLK = 0x0203;
		public const int WM_RBUTTONUP = 0x0205;

		public const uint NIM_ADD = 0;
		public const uint NIM_MODIFY = 1;
		public const uint NIM_DELETE = 2;

		[Flags]
		public enum NIF
		{
			NIF_MESSAGE = 0x00000001,
			NIF_ICON = 0x00000002,
			NIF_TIP = 0x00000004,
			NIF_STATE = 0x00000008,
			NIF_INFO = 0x00000010,
		}

		[DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
		public static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA pnid);
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct NOTIFYICONDATA
		{
			public int cbSize;
			public IntPtr hWnd;
			public int uID;
			public int uFlags;
			public int uCallbackMessage;
			public IntPtr hIcon;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x80)]
			public string szTip;
			public int dwState;
			public int dwStateMask;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x100)]
			public string szInfo;
			public int uTimeoutOrVersion;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x40)]
			public string szInfoTitle;
			public int dwInfoFlags;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct CREATESTRUCT
		{
			public IntPtr lpCreateParams;
			public IntPtr hInstance;
			public IntPtr hMenu;
			public IntPtr hwndParent;
			public int cy;
			public int cx;
			public int y;
			public int x;
			public int style;
			public IntPtr lpszName;
			public IntPtr lpszClass;
			public int dwExStyle;
		}

		[DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
		public static extern IntPtr CreateWindowEx(
		   int dwExStyle,
		   ushort regResult,
		   //string lpClassName,
		   string? lpWindowName,
		   uint dwStyle,
		   int x,
		   int y,
		   int nWidth,
		   int nHeight,
		   IntPtr hWndParent,
		   IntPtr hMenu,
		   IntPtr hInstance,
		   IntPtr lpParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool DestroyWindow(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern bool UpdateWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassExW")]
		public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);
		[DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)]
		public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct WNDCLASSEX
		{
			[MarshalAs(UnmanagedType.U4)]
			public int cbSize;
			[MarshalAs(UnmanagedType.U4)]
			public int style;
			public WndProc lpfnWndProc; // not WndProc
			public int cbClsExtra;
			public int cbWndExtra;
			public IntPtr hInstance;
			public IntPtr hIcon;
			public IntPtr hCursor;
			public IntPtr hbrBackground;
			public string lpszMenuName;
			public string lpszClassName;
			public IntPtr hIconSm;
		}
		public const uint CS_DBLCLKS = 8;

		public delegate IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
		public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", ExactSpelling = true)]
		public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
		public static extern IntPtr GetModuleHandle(string? lpModuleName);

		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool GetCursorPos(out POINT lpPoint);
		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				X = x;
				Y = y;
			}
		}

		[DllImport("user32.dll", SetLastError = true, EntryPoint = "GetMessageW")]
		public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
		[DllImport("user32.dll")]
		public static extern void PostQuitMessage(int nExitCode);
		//[DllImport("user32.dll", SetLastError = true)]
		//public static extern bool TranslateMessage([In] ref MSG lpMsg);
		[DllImport("user32.dll", SetLastError = true, EntryPoint = "DispatchMessageW")]
		public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);
		[StructLayout(LayoutKind.Sequential)]
		public struct MSG
		{
			public IntPtr hwnd;
			public uint message;
			public UIntPtr wParam;
			public IntPtr lParam;
			public int time;
			public POINT pt;
			public int lPrivate;
		}
	}
}
