using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.UiTests.Infrastructure;

[assembly: AvaloniaTestApplication(typeof(KyoshinEewViewer.UiTests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: AssemblyFixture(typeof(NetworkAssemblyFixture))]
[assembly: OfflineNetwork]

namespace KyoshinEewViewer.UiTests;

public static class TestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp()
	{
		NetworkIsolation.Require();
		NetworkAudit.Initialize();
		return AppBuilder.Configure<UiTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
	}
}

public partial class UiTestApp : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
		// 震度色などをアプリケーションのリソースから引く画面があるため、本番の起動処理と同じく登録する
		KyoshinEewViewerApp.Application = this;
	}
}
