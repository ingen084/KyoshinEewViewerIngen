using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Notification.Windows;

internal static class NativeMethods
{
	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool DestroyMenu(IntPtr hMenu);
	[DllImport("user32.dll")]
	public static extern IntPtr CreatePopupMenu();
	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "InsertMenuItemW")]
	public static extern bool InsertMenuItem(IntPtr hMenu, uint uItem, bool fByPosition, [In] ref Menuiteminfo lpmii);
	[DllImport("user32.dll")]
	public static extern IntPtr TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int reserved, IntPtr hwnd, IntPtr prcRect);
	[Flags]
	public enum TpmFlag
	{
		Leftalign = 0x0000,
		Rightbutton = 0x0002,
		Nonotify = 0x0080,
		Returncmd = 0x0100,
	}
	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMenuItemInfoW")]
	public static extern bool GetMenuItemInfo(IntPtr hMenu, uint uItem, bool fByPosition, [In, Out] ref Menuiteminfo lpmii);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct Menuiteminfo
	{
		public int cbSize;
		public Miim fMask;
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
	public enum Miim
	{
		Bitmap = 0x00000080,
		Checkmarks = 0x00000008,
		Data = 0x00000020,
		Ftype = 0x00000100,
		Id = 0x00000002,
		State = 0x00000001,
		String = 0x00000040,
		Submenu = 0x00000004,
		Type = 0x00000010
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
	public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
	   IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

	[DllImport("user32.dll")]
	public static extern bool DestroyIcon(IntPtr hIcon);

	public const int WmQuit = 0x0012;
	public const int WmUser = 0x0400;
	public const int WmTrayCallbackMessage = WmUser + 1;
	public const int WmCommand = 0x0111;

	public const int WmLbuttonup = 0x0202;
	public const int WmLbuttondblclk = 0x0203;
	public const int WmRbuttonup = 0x0205;

	public const uint NimAdd = 0;
	public const uint NimModify = 1;
	public const uint NimDelete = 2;

	[Flags]
	public enum Nif
	{
		NifMessage = 0x00000001,
		NifIcon = 0x00000002,
		NifTip = 0x00000004,
		NifState = 0x00000008,
		NifInfo = 0x00000010,
	}

	[DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
	public static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref Notifyicondata pnid);
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct Notifyicondata
	{
		public int cbSize;
		public IntPtr hWnd;
		public int uID;
		public Nif uFlags;
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
	public static extern ushort RegisterClassEx([In] ref Wndclassex lpWndClass);
	[DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)]
	public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct Wndclassex
	{
		[MarshalAs(UnmanagedType.U4)]
		public int cbSize;
		[MarshalAs(UnmanagedType.U4)]
		public int style;
		public WndProc lpfnWndProc;
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
	public const uint CsDblclks = 8;

	public delegate IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
	public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll", EntryPoint = "SendMessageW")]
	public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIdEvent, uint uElapse, IntPtr lpTimerFunc);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
	public static extern IntPtr GetModuleHandle(string? lpModuleName);

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(IntPtr hWnd);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool GetCursorPos(out Point lpPoint);
	[StructLayout(LayoutKind.Sequential)]
	public struct Point
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll", SetLastError = true, EntryPoint = "GetMessageW")]
	public static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
	[DllImport("user32.dll", SetLastError = true, EntryPoint = "DispatchMessageW")]
	public static extern IntPtr DispatchMessage([In] ref Msg lpMsg);
	[StructLayout(LayoutKind.Sequential)]
	public struct Msg
	{
		public IntPtr hwnd;
		public uint message;
		public UIntPtr wParam;
		public IntPtr lParam;
		public int time;
		public Point pt;
		public int lPrivate;
	}
}
