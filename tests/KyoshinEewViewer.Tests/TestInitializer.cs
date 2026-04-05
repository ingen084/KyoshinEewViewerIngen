using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace KyoshinEewViewer.Tests;

internal static class TestInitializer
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		RxAppBuilder.CreateReactiveUIBuilder()
			.WithCoreServices()
			.BuildApp();
	}
}
