using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using KyoshinEewViewer.Core;

namespace CustomRenderItemTest.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
	protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
		=> base.CustomizeAppBuilder(builder)
			.UseKeviFonts();
}
