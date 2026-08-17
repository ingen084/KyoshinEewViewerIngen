using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.Lightning;
using KyoshinEewViewer.Series.ObservationPointEditor;
using KyoshinEewViewer.Series.Qzss;
using KyoshinEewViewer.Series.Qzss.Services;
using KyoshinEewViewer.Series.Radar;
using KyoshinEewViewer.Series.ShakeDetectionVerifier;
using KyoshinEewViewer.Series.Tsunami;
using KyoshinEewViewer.Series.Typhoon;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.ExternalPublishers.Axis;
using KyoshinEewViewer.Services.Feedback;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
using KyoshinEewViewer.Services.Workflows;
using KyoshinEewViewer.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace KyoshinEewViewer;

public static class KyoshinEewViewerApp
{
	public static Application? Application { get; set; }
	public static TopLevel? TopLevelControl { get; set; }
	public static ThemeSelector? Selector { get; set; }

	/// <summary>
	/// 全プラットフォーム共通のサービスを DI コンテナへ登録する。
	/// 呼び出し前に <see cref="LoggingAdapter.Setup"/> を済ませておくこと。
	/// </summary>
	public static IServiceCollection AddKyoshinEewViewer(this IServiceCollection services)
	{
		// ロギング
		if (LoggingAdapter.Factory is { } factory)
			services.AddSingleton(factory);
		services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
		if (LoggingAdapter.InMemoryProvider is { } inMemoryProvider)
			services.AddSingleton(inMemoryProvider);

		// 基盤
		services.AddSingleton(_ => new SeriesController());
		services.AddSingleton<TimerService>();
		services.AddSingleton<InformationCacheService>();
		services.AddSingleton<NotificationService>();
		services.AddSingleton<SoundPlayerService>();
		services.AddSingleton<UpdateCheckService>();
		services.AddSingleton<VoicevoxService>();
		services.AddSingleton<WorkflowService>();
		services.AddSingleton<TelegramProvideService>();
		services.AddSingleton<ObservationPointsUpdateService>();

		// 電文･外部連携
		services.AddSingleton<DmdataRedundantTelegramPublisher>();
		services.AddSingleton<JmaXmlTelegramPublisher>();
		services.AddSingleton<AxisInformationProvider>();

		// 設定ページ
		services.AddSingleton<DmdataSettingPage>();
		services.AddSingleton<AxisSettingPage>();
		services.AddSingleton<FeedbackSettingPage>();

		// Series
		services.AddSingleton<EarthquakeWatchService>();
		services.AddSingleton<EarthquakeSeries>();
		services.AddSingleton<KyoshinMonitorSeries>();
		services.AddSingleton<TsunamiSeries>();
		services.AddSingleton<TyphoonSeries>();
		services.AddSingleton<RadarSeries>();
		services.AddSingleton<LightningSeries>();
		services.AddSingleton<SerialConnector>();
		services.AddSingleton<QzssSeries>();
		services.AddSingleton<ShakeDetectionVerifierSeries>();
		services.AddSingleton<ObservationPointEditorSeries>();

		// ViewModel
		services.AddSingleton<MainViewModel>();
		services.AddSingleton<SettingWindowViewModel>();
		services.AddSingleton<SetupWizardWindowViewModel>();
		services.AddSingleton<DebugWindowViewModel>();

		return services;
	}

	/// <summary>
	/// 設定の読み込みとロギングの初期化を行い、DI コンテナへ設定を登録する。
	/// ロギングの初期化に設定が必要なため、コンテナ構築より前に実行する
	/// </summary>
	public static KyoshinEewViewerConfiguration SetupConfigurationAndLogging(
		this IServiceCollection services,
		Action<KyoshinEewViewerConfiguration>? configure = null)
	{
		var config = ConfigurationLoader.Load();
		configure?.Invoke(config);
		LoggingAdapter.Setup(config);
		services.AddSingleton(config);
		return config;
	}
}
