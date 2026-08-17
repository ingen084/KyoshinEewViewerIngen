using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinEewViewer.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.ViewModels;

public partial class DebugWindowViewModel : ViewModelBase, IDisposable
{
	public string Title => "デバッグウィンドウ";

	public KyoshinEewViewerConfiguration Config { get; }

	private bool _isActive;
	private readonly InMemoryLoggerProvider? _loggerProvider;

	[ObservableProperty]
	public partial ObservableCollection<LayerMetricsViewModel> LayerMetrics { get; set; } = [];

	[ObservableProperty]
	public partial string TotalFrameTime { get; set; } = "-";

	[ObservableProperty]
	public partial string Zoom { get; set; } = "-";

	[ObservableProperty]
	public partial string IsNavigating { get; set; } = "-";

	[ObservableProperty]
	public partial string Timestamp { get; set; } = "-";

	[ObservableProperty]
	public partial ObservableCollection<LogEntryViewModel> LogEntries { get; set; } = [];

	[ObservableProperty]
	public partial bool AutoScroll { get; set; } = true;

	[ObservableProperty]
	public partial bool ScrollToEnd { get; set; }

	public DebugWindowViewModel(KyoshinEewViewerConfiguration config, InMemoryLoggerProvider? loggerProvider = null)
	{
		Config = config;
		_loggerProvider = loggerProvider ?? ServiceLocator.Current.GetService<InMemoryLoggerProvider>();

		// メトリクス更新イベントをサブスクライブ (UI スレッドへマーシャリングする)
		StrongReferenceMessenger.Default.Register<MetricsUpdated>(this,
			(_, msg) => Dispatcher.UIThread.Post(() => UpdateMetrics(msg.Metrics)));

		// ログ追加イベントをサブスクライブ (UI スレッドへマーシャリングする)
		StrongReferenceMessenger.Default.Register<LogEntryAdded>(this,
			(_, msg) => Dispatcher.UIThread.Post(() => AddLogEntry(msg.Entry)));

		// 初回ログ読み込み
		LoadInitialLogs();

		// ウィンドウがアクティブになったらメトリクス収集を有効化
		Activate();
	}

	/// <summary>
	/// 初回ログ読み込み
	/// </summary>
	private void LoadInitialLogs()
	{
		if (_loggerProvider == null)
			return;

		var logs = _loggerProvider.GetLogs();
		foreach (var log in logs)
		{
			AddLogEntry(log);
		}
	}

	/// <summary>
	/// ログをクリア
	/// </summary>
	public void ClearLogs()
	{
		_loggerProvider?.Clear();
		LogEntries.Clear();
	}

	/// <summary>
	/// 地図画像を保存する
	/// </summary>
	public async Task SaveMapImageAsync()
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window window)
			return;

		var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "地図画像を保存",
			DefaultExtension = "skp",
			SuggestedFileName = $"map_{DateTime.Now:yyyyMMdd_HHmmss}.skp",
			FileTypeChoices =
			[
				new FilePickerFileType("SKPicture") { Patterns = ["*.skp"] },
				new FilePickerFileType("PNG画像") { Patterns = ["*.png"] },
				new FilePickerFileType("JPEG画像") { Patterns = ["*.jpg", "*.jpeg"] },
				new FilePickerFileType("WEBP画像") { Patterns = ["*.webp"] }
			]
		});

		if (file is null)
			return;

		StrongReferenceMessenger.Default.Send(new MapImageSaveRequested { TargetPath = file.Path.LocalPath });
	}

	/// <summary>
	/// メトリクス収集を有効化
	/// </summary>
	public void Activate()
	{
		if (_isActive) return;
		_isActive = true;
		StrongReferenceMessenger.Default.Send(new MetricsEnabledChanged { IsEnabled = true });
	}

	/// <summary>
	/// メトリクス収集を無効化
	/// </summary>
	public void Deactivate()
	{
		if (!_isActive) return;
		_isActive = false;
		StrongReferenceMessenger.Default.Send(new MetricsEnabledChanged { IsEnabled = false });
	}

	public void Dispose()
	{
		Deactivate();
		StrongReferenceMessenger.Default.UnregisterAll(this);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// ログエントリを追加
	/// </summary>
	private void AddLogEntry(LogEntry log)
	{
		LogEntries.Add(new LogEntryViewModel
		{
			Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
			LogLevel = log.LogLevel.ToString(),
			Category = GetShortCategoryName(log.CategoryName),
			Message = log.Message,
			Exception = log.Exception?.ToString()
		});

		// 最大1000件に制限
		while (LogEntries.Count > 1000)
			LogEntries.RemoveAt(0);

		// 自動スクロールが有効な場合、スクロールをトリガー
		if (AutoScroll)
			TriggerScrollToEnd();
	}

	/// <summary>
	/// スクロールを最下部にトリガー
	/// </summary>
	private void TriggerScrollToEnd()
	{
		// トグルしてバインディングをトリガー
		ScrollToEnd = false;
		ScrollToEnd = true;
	}

	private static string GetShortCategoryName(string categoryName)
	{
		// 最後のドット以降のみを取得
		var lastDot = categoryName.LastIndexOf('.');
		return lastDot >= 0 ? categoryName[(lastDot + 1)..] : categoryName;
	}

	private void UpdateMetrics(FrameRenderMetrics? latest)
	{
		if (latest == null)
		{
			TotalFrameTime = "-";
			Zoom = "-";
			IsNavigating = "-";
			Timestamp = "-";
			LayerMetrics.Clear();
			return;
		}

		TotalFrameTime = $"{latest.TotalFrameTime.TotalMilliseconds:F2} ms";
		Zoom = $"{latest.Zoom:F2}";
		IsNavigating = latest.IsNavigating ? "はい" : "いいえ";
		Timestamp = latest.Timestamp.ToString("yyyy/MM/dd HH:mm:ss");

		LayerMetrics.Clear();
		foreach (var layer in latest.LayerMetrics)
		{
			var vm = new LayerMetricsViewModel
			{
				LayerName = layer.LayerName,
				RenderTime = $"{layer.RenderTime.TotalMilliseconds:F2} ms",
				RenderInfo = layer.RenderInfo != null
					? string.Join(", ", layer.RenderInfo.Select(kv => $"{kv.Key}: {kv.Value}"))
					: "-"
			};
			LayerMetrics.Add(vm);
		}
	}
}

public partial class LayerMetricsViewModel : ObservableObject
{
	[ObservableProperty]
	public partial string LayerName { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string RenderTime { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string RenderInfo { get; set; } = string.Empty;
}

public partial class LogEntryViewModel : ObservableObject
{
	[ObservableProperty]
	public partial string Timestamp { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string LogLevel { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Category { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Message { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? Exception { get; set; }
}
