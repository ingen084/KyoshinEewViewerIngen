using System;
using System.Runtime.InteropServices;

namespace CustomRenderItemTest;

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
}
