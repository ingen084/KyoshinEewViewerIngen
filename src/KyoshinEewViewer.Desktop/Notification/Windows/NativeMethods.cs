#if WINDOWS
using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Desktop.Notification.Windows;

internal static class NativeMethods
{
	// Toast のクリック活性化を受け取る COM アクティベーターを登録・解除する
	public const uint ClsctxLocalServer = 4;
	public const uint RegclsMultipleuse = 1;

	[DllImport("ole32.dll")]
	public static extern int CoRegisterClassObject(
		[MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
		[MarshalAs(UnmanagedType.IUnknown)] object pUnk,
		uint dwClsContext,
		uint flags,
		out uint lpdwRegister);

	[DllImport("ole32.dll")]
	public static extern int CoRevokeClassObject(uint dwRegister);
}
#endif
