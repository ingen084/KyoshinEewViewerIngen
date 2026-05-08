using ReactiveUI.Builder;
using System.Runtime.CompilerServices;

namespace KyoshinEewViewer.Tests;

internal static class TestModuleInit
{
	[ModuleInitializer]
	public static void Init()
	{
		RxAppBuilder.CreateReactiveUIBuilder()
			.WithCoreServices()
			.BuildApp();
	}
}
