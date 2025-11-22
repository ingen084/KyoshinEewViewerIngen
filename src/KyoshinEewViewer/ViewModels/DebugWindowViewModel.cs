using KyoshinEewViewer.Events;
using KyoshinEewViewer.Core.Models.Metrics;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using KyoshinEewViewer.Core.Models.Events;

namespace KyoshinEewViewer.ViewModels;

public class DebugWindowViewModel : ViewModelBase, IDisposable
{
	public string Title => "デバッグウィンドウ";

	private IDisposable? _metricsSubscription;
	private bool _isActive;

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

	public DebugWindowViewModel()
	{
		SplatRegistrations.RegisterLazySingleton<DebugWindowViewModel>();

		// メトリクス更新イベントをサブスクライブ
		_metricsSubscription = MessageBus.Current.Listen<MetricsUpdated>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(msg => UpdateMetrics(msg.Metrics));

		// ウィンドウがアクティブになったらメトリクス収集を有効化
		Activate();
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
		GC.SuppressFinalize(this);
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
