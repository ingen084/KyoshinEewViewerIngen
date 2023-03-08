using System;
using System.Runtime.InteropServices;

namespace CustomRenderItemTest;

internal static class NativeMethods
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int attrValue, int attrSize);
	public enum DWMWINDOWATTRIBUTE
	{
		DWMWA_NCRENDERING_ENABLED,
		DWMWA_NCRENDERING_POLICY,
		DWMWA_TRANSITIONS_FORCEDISABLED,
		DWMWA_ALLOW_NCPAINT,
		DWMWA_CAPTION_BUTTON_BOUNDS,
		DWMWA_NONCLIENT_RTL_LAYOUT,
		DWMWA_FORCE_ICONIC_REPRESENTATION,
		DWMWA_FLIP3D_POLICY,
		DWMWA_EXTENDED_FRAME_BOUNDS,
		DWMWA_HAS_ICONIC_BITMAP,
		DWMWA_DISALLOW_PEEK,
		DWMWA_EXCLUDED_FROM_PEEK,
		DWMWA_CLOAK,
		DWMWA_CLOAKED,
		DWMWA_FREEZE_REPRESENTATION,
		DWMWA_PASSIVE_UPDATE_MODE,
		DWMWA_USE_HOSTBACKDROPBRUSH,
		DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
		DWMWA_WINDOW_CORNER_PREFERENCE = 33,
		DWMWA_BORDER_COLOR,
		DWMWA_CAPTION_COLOR,
		DWMWA_TEXT_COLOR,
		DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
		DWMWA_LAST
	};

	[Flags]
	public enum RestartFlags
	{
		NONE = 0,
		RESTART_NO_CRASH = 1,
		RESTART_NO_HANG = 2,
		RESTART_NO_PATCH = 4,
		RESTART_NO_REBOOT = 8,
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern uint RegisterApplicationRestart(string pwsCommandLine, RestartFlags dwFlags);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern uint WerRegisterAppLocalDump(string localAppDataRelativePath);
}
