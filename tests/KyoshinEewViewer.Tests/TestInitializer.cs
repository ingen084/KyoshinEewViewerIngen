using ReactiveUI.Builder;
using System.Runtime.CompilerServices;

namespace KyoshinEewViewer.Tests;

internal static class TestInitializer
{
	/// <summary>
	/// ReactiveUI 12 以降は明示的な初期化が必須になっており、
	/// アプリ側は Avalonia の UseReactiveUI() で初期化されるがテストホストでは実行されない。
	/// WhenAnyValue 等を使用するコードが TypeInitializationException で失敗するため、
	/// テストアセンブリの読み込み時に一度だけ初期化する。
	/// </summary>
	[ModuleInitializer]
	internal static void Initialize()
		=> RxAppBuilder.CreateReactiveUIBuilder()
			.WithCoreServices()
			.BuildApp();
}
