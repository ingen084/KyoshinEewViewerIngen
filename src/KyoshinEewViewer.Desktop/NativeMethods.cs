using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Desktop;

internal static class NativeMethods
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, Dwmwindowattribute attr, ref int attrValue, int attrSize);
	public enum Dwmwindowattribute
	{
		DwmwaNcrenderingEnabled,
		DwmwaNcrenderingPolicy,
		DwmwaTransitionsForcedisabled,
		DwmwaAllowNcpaint,
		DwmwaCaptionButtonBounds,
		DwmwaNonclientRtlLayout,
		DwmwaForceIconicRepresentation,
		DwmwaFlip3DPolicy,
		DwmwaExtendedFrameBounds,
		DwmwaHasIconicBitmap,
		DwmwaDisallowPeek,
		DwmwaExcludedFromPeek,
		DwmwaCloak,
		DwmwaCloaked,
		DwmwaFreezeRepresentation,
		DwmwaPassiveUpdateMode,
		DwmwaUseHostbackdropbrush,
		DwmwaUseImmersiveDarkMode = 20,
		DwmwaWindowCornerPreference = 33,
		DwmwaBorderColor,
		DwmwaCaptionColor,
		DwmwaTextColor,
		DwmwaVisibleFrameBorderThickness,
		DwmwaLast
	};

	[Flags]
	public enum RestartFlags
	{
		None = 0,
		RestartNoCrash = 1,
		RestartNoHang = 2,
		RestartNoPatch = 4,
		RestartNoReboot = 8,
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern uint RegisterApplicationRestart(string pwsCommandLine, RestartFlags dwFlags);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern uint WerRegisterAppLocalDump(string localAppDataRelativePath);

	/// <summary>
	/// EnumDisplayDevices でデバイスパス (デバイスインターフェイス名) を取得する
	/// </summary>
	public const uint EddGetDeviceInterfaceName = 1;

	[DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

	[DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool EnumDisplayDevices(string? deviceName, uint deviceIndex, ref DisplayDevice device, uint flags);

	[StructLayout(LayoutKind.Sequential)]
	public struct NativeRect
	{
		public int Left, Top, Right, Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct MonitorInfoEx
	{
		public int Size;
		public NativeRect Monitor;
		public NativeRect Work;
		public uint Flags;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string? DeviceName;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct DisplayDevice
	{
		public int Size;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string? DeviceName;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string? DeviceString;
		public uint StateFlags;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string? DeviceId;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string? DeviceKey;
	}
}
