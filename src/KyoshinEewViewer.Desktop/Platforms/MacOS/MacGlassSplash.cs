using Avalonia.Controls;
using Avalonia.Platform;
using KyoshinEewViewer.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Versioning;

namespace KyoshinEewViewer.Desktop.Platforms.MacOS;

/// <summary>
/// ガラスと透明PNGの前景を同じネイティブビュー内に配置する。
/// Avaloniaの不透明な背景や、NativeControlHostをまたぐ描画順に依存しない。
/// </summary>
[SupportedOSPlatform("macos26.0")]
internal sealed class MacGlassSplash(Action<bool> setGlassActive) : NativeControlHost
{
	private IntPtr _glass;
	private IntPtr _imageView;
	private IntPtr _lightImage;
	private IntPtr _darkImage;
	private bool _dark;

	public void SetDark(bool dark)
	{
		_dark = dark;
		if (_glass == IntPtr.Zero)
			return;
		using var pool = new MacNative.AutoreleasePool();
		ApplyAppearance();
	}

	protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
	{
		try
		{
			using var pool = new MacNative.AutoreleasePool();
			// 旧OSや「透明度を下げる」の場合は背面の静止画をそのまま使う。
			if (MacNative.GetClass("NSGlassEffectView") == IntPtr.Zero || MacNative.ReduceTransparency)
			{
				setGlassActive(false);
				return base.CreateNativeControlCore(parent);
			}

			_lightImage = MacNative.LoadImage("splash-foreground-light", 620, 300);
			_darkImage = MacNative.LoadImage("splash-foreground-dark", 620, 300);
			_glass = MacNative.CreateView("NSGlassEffectView", 620, 300);
			_imageView = MacNative.CreateView("NSImageView", 620, 300);
			MacNative.SetDouble(_glass, "setCornerRadius:", 24);
			// NSViewWidthSizable | NSViewHeightSizable。Retinaでもポイント単位で配置。
			MacNative.SetInteger(_imageView, "setAutoresizingMask:", 2 | 16);
			MacNative.SetInteger(_imageView, "setImageScaling:", 3); // NSImageScaleProportionallyUpOrDown
			MacNative.Set(_imageView, "setAccessibilityLabel:", MacNative.String("Kyoshin Eew Viewer 起動中"));
			MacNative.Set(_glass, "setContentView:", _imageView);
			ApplyAppearance();
			setGlassActive(true);
			return new PlatformHandle(_glass, "NSView");
		}
		catch (Exception ex)
		{
			ReleaseNativeResources();
			setGlassActive(false);
			AppLog.Default.LogWarning(ex, "Liquid Glassスプラッシュを作成できないため静止画を使用します");
			return base.CreateNativeControlCore(parent);
		}
	}

	private void ApplyAppearance()
	{
		MacNative.SetAppearance(_glass, _dark);
		MacNative.Set(_imageView, "setImage:", _dark ? _darkImage : _lightImage);
	}

	protected override void DestroyNativeControlCore(IPlatformHandle control)
	{
		if (_glass != IntPtr.Zero && control.Handle == _glass)
		{
			// NativeControlHostがビューを取り外してから、alloc/initで持った参照を解放する。
			ReleaseNativeResources();
			setGlassActive(false);
		}
		else
			base.DestroyNativeControlCore(control);
	}

	private void ReleaseNativeResources()
	{
		MacNative.Release(ref _glass);
		MacNative.Release(ref _imageView);
		MacNative.Release(ref _lightImage);
		MacNative.Release(ref _darkImage);
	}
}
