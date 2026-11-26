using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Notification;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DI 登録の検証。
/// MS.DI は未登録の引数を nullable でも解決できず例外にするため、
/// プラットフォーム固有サービスを持たない構成でも解決できることを保証する。
/// </summary>
public class ServiceRegistrationTests
{
	private static ServiceCollection CreateBaseServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton(new KyoshinEewViewerConfiguration());
		services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(_ => { }));
		services.AddKyoshinEewViewer();
		return services;
	}

	[Fact(DisplayName = "デスクトップ相当の構成ですべてのサービスが解決できる")]
	public void デスクトップ構成で解決できる()
	{
		var services = CreateBaseServices();
		services.AddSingleton<ISubWindowsService>(_ => null!);
		services.AddSingleton<NotificationProvider>(_ => null!);
		services.AddSingleton<TrayIconProvider>(_ => null!);

		services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
	}

	[Fact(DisplayName = "プラットフォーム固有サービスがない構成でもすべてのサービスが解決できる")]
	public void 最小構成で解決できる()
		// Android / Browser / Benchmark は ISubWindowsService 等を登録しない
		=> CreateBaseServices().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
