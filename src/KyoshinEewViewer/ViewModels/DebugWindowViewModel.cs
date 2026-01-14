using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class DebugWindowViewModel : ViewModelBase, IDisposable
{
	public string Title => "デバッグウィンドウ";

	public KyoshinEewViewerConfiguration Config { get; }

	private IDisposable? _metricsSubscription;
	private IDisposable? _logSubscription;
	private bool _isActive;
	private readonly InMemoryLoggerProvider? _loggerProvider;

	private ObservableCollection<LayerMetricsViewModel> _layerMetrics = [];
	public ObservableCollection<LayerMetricsViewModel> LayerMetrics
	{
		get => _layerMetrics;
		set => this.RaiseAndSetIfChanged(ref _layerMetrics, value);
	}

	private string _totalFrameTime = "-";
	public string TotalFrameTime
	{
		get => _totalFrameTime;
		set => this.RaiseAndSetIfChanged(ref _totalFrameTime, value);
	}

	private string _zoom = "-";
	public string Zoom
	{
		get => _zoom;
		set => this.RaiseAndSetIfChanged(ref _zoom, value);
	}

	private string _isNavigating = "-";
	public string IsNavigating
	{
		get => _isNavigating;
		set => this.RaiseAndSetIfChanged(ref _isNavigating, value);
	}

	private string _timestamp = "-";
	public string Timestamp
	{
		get => _timestamp;
		set => this.RaiseAndSetIfChanged(ref _timestamp, value);
	}

	private ObservableCollection<LogEntryViewModel> _logEntries = [];
	public ObservableCollection<LogEntryViewModel> LogEntries
	{
		get => _logEntries;
		set => this.RaiseAndSetIfChanged(ref _logEntries, value);
	}

	private bool _autoScroll = true;
	public bool AutoScroll
	{
		get => _autoScroll;
		set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
	}

	private bool _scrollToEnd;
	public bool ScrollToEnd
	{
		get => _scrollToEnd;
		set => this.RaiseAndSetIfChanged(ref _scrollToEnd, value);
	}

	public DebugWindowViewModel(KyoshinEewViewerConfiguration config, InMemoryLoggerProvider? loggerProvider = null)
	{
		SplatRegistrations.RegisterLazySingleton<DebugWindowViewModel>();

		Config = config;
		_loggerProvider = loggerProvider ?? Locator.Current.GetService<InMemoryLoggerProvider>();

		// メトリクス更新イベントをサブスクライブ
		_metricsSubscription = MessageBus.Current.Listen<MetricsUpdated>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(msg => UpdateMetrics(msg.Metrics));

		// ログ追加イベントをサブスクライブ
		_logSubscription = MessageBus.Current.Listen<LogEntryAdded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(msg => AddLogEntry(msg.Entry));

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

		MessageBus.Current.SendMessage(new MapImageSaveRequested { TargetPath = file.Path.LocalPath });
	}

	/// <summary>
	/// メトリクス収集を有効化
	/// </summary>
	public void Activate()
	{
		if (_isActive) return;
		_isActive = true;
		MessageBus.Current.SendMessage(new MetricsEnabledChanged { IsEnabled = true });
	}

	/// <summary>
	/// メトリクス収集を無効化
	/// </summary>
	public void Deactivate()
	{
		if (!_isActive) return;
		_isActive = false;
		MessageBus.Current.SendMessage(new MetricsEnabledChanged { IsEnabled = false });
	}

	public void Dispose()
	{
		Deactivate();
		_metricsSubscription?.Dispose();
		_logSubscription?.Dispose();
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

public class LayerMetricsViewModel : ReactiveObject
{
	private string _layerName = string.Empty;
	public string LayerName
	{
		get => _layerName;
		set => this.RaiseAndSetIfChanged(ref _layerName, value);
	}

	private string _renderTime = string.Empty;
	public string RenderTime
	{
		get => _renderTime;
		set => this.RaiseAndSetIfChanged(ref _renderTime, value);
	}

	private string _renderInfo = string.Empty;
	public string RenderInfo
	{
		get => _renderInfo;
		set => this.RaiseAndSetIfChanged(ref _renderInfo, value);
	}
}

public class LogEntryViewModel : ReactiveObject
{
	private string _timestamp = string.Empty;
	public string Timestamp
	{
		get => _timestamp;
		set => this.RaiseAndSetIfChanged(ref _timestamp, value);
	}

	private string _logLevel = string.Empty;
	public string LogLevel
	{
		get => _logLevel;
		set => this.RaiseAndSetIfChanged(ref _logLevel, value);
	}

	private string _category = string.Empty;
	public string Category
	{
		get => _category;
		set => this.RaiseAndSetIfChanged(ref _category, value);
	}

	private string _message = string.Empty;
	public string Message
	{
		get => _message;
		set => this.RaiseAndSetIfChanged(ref _message, value);
	}

	private string? _exception;
	public string? Exception
	{
		get => _exception;
		set => this.RaiseAndSetIfChanged(ref _exception, value);
	}
}
