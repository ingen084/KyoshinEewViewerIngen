using Avalonia.Platform;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KyoshinEewViewer.Desktop.Platforms.MacOS;

/// <summary>スプラッシュに必要なAppKit呼び出し。すべてUIスレッド上で使う。</summary>
[SupportedOSPlatform("macos")]
internal static class MacNative
{
	private const string ObjC = "/usr/lib/libobjc.A.dylib";

	[DllImport(ObjC, EntryPoint = "objc_getClass")]
	internal static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
	[DllImport(ObjC, EntryPoint = "sel_registerName")]
	private static extern IntPtr Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr value);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern IntPtr SendString(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern IntPtr SendBytes(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern IntPtr SendRect(IntPtr receiver, IntPtr selector, NativeRect rect);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern void SendSize(IntPtr receiver, IntPtr selector, NativeSize size);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern void SendInteger(IntPtr receiver, IntPtr selector, nint value);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	private static extern void SendDouble(IntPtr receiver, IntPtr selector, double value);
	[DllImport(ObjC, EntryPoint = "objc_msgSend")]
	[return: MarshalAs(UnmanagedType.I1)]
	private static extern bool SendBoolResult(IntPtr receiver, IntPtr selector);

	// CGFloat is double on both supported macOS architectures (x64 / arm64).
	[StructLayout(LayoutKind.Sequential)]
	private readonly struct NativeRect(double x, double y, double width, double height)
	{
		private readonly double _x = x, _y = y, _width = width, _height = height;
	}
	[StructLayout(LayoutKind.Sequential)]
	private readonly struct NativeSize(double width, double height)
	{
		private readonly double _width = width, _height = height;
	}

	internal static IntPtr Get(IntPtr receiver, string selector) => Send(receiver, Selector(selector));
	internal static void Set(IntPtr receiver, string selector, IntPtr value) => Send(receiver, Selector(selector), value);
	internal static void SetInteger(IntPtr receiver, string selector, nint value) => SendInteger(receiver, Selector(selector), value);
	internal static void SetDouble(IntPtr receiver, string selector, double value) => SendDouble(receiver, Selector(selector), value);
	internal static IntPtr String(string value) => SendString(GetClass("NSString"), Selector("stringWithUTF8String:"), value);

	internal static IntPtr CreateView(string className, double width, double height)
	{
		var cls = GetClass(className);
		if (cls == IntPtr.Zero)
			throw new PlatformNotSupportedException(className);
		var view = SendRect(Get(cls, "alloc"), Selector("initWithFrame:"), new(0, 0, width, height));
		return view != IntPtr.Zero ? view : throw new InvalidOperationException($"{className}の生成に失敗しました");
	}

	internal static void SetAppearance(IntPtr view, bool dark)
	{
		var appearance = Send(GetClass("NSAppearance"), Selector("appearanceNamed:"),
			String(dark ? "NSAppearanceNameDarkAqua" : "NSAppearanceNameAqua"));
		Set(view, "setAppearance:", appearance);
	}

	internal static bool ReduceTransparency =>
		SendBoolResult(Get(GetClass("NSWorkspace"), "sharedWorkspace"), Selector("accessibilityDisplayShouldReduceTransparency"));

	internal static unsafe IntPtr LoadImage(string assetName, double width, double height)
	{
		using var stream = AssetLoader.Open(new Uri($"avares://KyoshinEewViewer.Desktop/Assets/MacOS/{assetName}.png"));
		using var buffer = new MemoryStream();
		stream.CopyTo(buffer);
		var bytes = buffer.ToArray();
		IntPtr data;
		fixed (byte* pointer = bytes)
			data = SendBytes(Get(GetClass("NSData"), "alloc"), Selector("initWithBytes:length:"), (IntPtr)pointer, (nuint)bytes.Length);
		if (data == IntPtr.Zero)
			throw new InvalidOperationException("NSDataの生成に失敗しました");
		try
		{
			var image = Send(Get(GetClass("NSImage"), "alloc"), Selector("initWithData:"), data);
			if (image == IntPtr.Zero)
				throw new InvalidOperationException($"画像を読み込めません: {assetName}");
			SendSize(image, Selector("setSize:"), new(width, height));
			return image; // alloc/initの所有権を呼び出し側に渡す。
		}
		finally
		{
			Release(ref data);
		}
	}

	internal static void Release(ref IntPtr value)
	{
		if (value == IntPtr.Zero)
			return;
		Get(value, "release");
		value = IntPtr.Zero;
	}

	internal sealed class AutoreleasePool : IDisposable
	{
		private IntPtr _pool = Get(Get(GetClass("NSAutoreleasePool"), "alloc"), "init");
		public void Dispose() => Release(ref _pool);
	}
}
