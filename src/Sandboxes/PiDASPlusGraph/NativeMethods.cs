using System;
using System.Runtime.InteropServices;

namespace PiDASPlusGraph;

internal static class NativeMethods
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, Dwmwindowattribute attr, ref int attrValue, int attrSize);

	public enum Dwmwindowattribute
	{
		DwmwaUseImmersiveDarkMode = 20,
		DwmwaWindowCornerPreference = 33,
		DwmwaBorderColor,
		DwmwaCaptionColor,
		DwmwaTextColor,
	}
}
