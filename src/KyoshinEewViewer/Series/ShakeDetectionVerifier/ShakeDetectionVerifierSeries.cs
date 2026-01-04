using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.Core.ShakeDetection;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier;

public class ShakeDetectionVerifierSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(
		typeof(ShakeDetectionVerifierSeries),
		"shake-detection-verifier",
		"揺れ検知",
		new FontIconSource { Glyph = "\xe13a", FontFamily = new(Utils.IconFontName) },
		false,
		"揺れ検知のパラメータを調整し、比較検証を行います。"
	);

	private ShakeDetectionVerifierView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public override ISettingPage[] SettingPages => [];

	private KyoshinEewViewerConfiguration Config { get; }
	private ObservationPointsUpdateService ObservationPointsUpdateService { get; }
	private ILogger Logger { get; }
	private MapData? MapData { get; set; }

	#region リプレイデータ
	private ReplayFileHeader? _loadedHeader;
	public ReplayFileHeader? LoadedHeader
	{
		get => _loadedHeader;
		set => this.RaiseAndSetIfChanged(ref _loadedHeader, value);
	}

	private ReplayData[]? _loadedData;
	public ReplayData[]? LoadedData
	{
		get => _loadedData;
		set => this.RaiseAndSetIfChanged(ref _loadedData, value);
	}

	private bool _isDataLoaded;
	public bool IsDataLoaded
	{
		get => _isDataLoaded;
		set => this.RaiseAndSetIfChanged(ref _isDataLoaded, value);
	}

	private string _statusMessage = "リプレイファイルを読み込んでください";
	public string StatusMessage
	{
		get => _statusMessage;
		set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
	}
	#endregion

	#region 時刻制御
	private int _currentFrameIndex;
	public int CurrentFrameIndex
	{
		get => _currentFrameIndex;
		set
		{
			if (_currentFrameIndex == value)
				return;
			this.RaiseAndSetIfChanged(ref _currentFrameIndex, value);
			UpdateCurrentTime();
		}
	}

	private int _maxFrameIndex;
	public int MaxFrameIndex
	{
		get => _maxFrameIndex;
		set => this.RaiseAndSetIfChanged(ref _maxFrameIndex, value);
	}

	private DateTime _currentTime;
	public DateTime CurrentTime
	{
		get => _currentTime;
		set => this.RaiseAndSetIfChanged(ref _currentTime, value);
	}

	private string _currentTimeText = "--:--:--";
	public string CurrentTimeText
	{
		get => _currentTimeText;
		set => this.RaiseAndSetIfChanged(ref _currentTimeText, value);
	}
	#endregion

	#region パラメータ（左側）
	private ShakeDetectionParameters _leftParameters = ShakeDetectionParameters.Default;
	public ShakeDetectionParameters LeftParameters
	{
		get => _leftParameters;
		set => this.RaiseAndSetIfChanged(ref _leftParameters, value);
	}
	#endregion

	#region パラメータ（右側）
	private ShakeDetectionParameters _rightParameters = ShakeDetectionParameters.Default;
	public ShakeDetectionParameters RightParameters
	{
		get => _rightParameters;
		set => this.RaiseAndSetIfChanged(ref _rightParameters, value);
	}
	#endregion

	#region オーバーレイ表示制御
	private bool _showLeftParameterPanel;
	/// <summary>
	/// 左側のパラメータパネルを表示するか
	/// </summary>
	public bool ShowLeftParameterPanel
	{
		get => _showLeftParameterPanel;
		set => this.RaiseAndSetIfChanged(ref _showLeftParameterPanel, value);
	}

	private bool _showRightParameterPanel;
	/// <summary>
	/// 右側のパラメータパネルを表示するか
	/// </summary>
	public bool ShowRightParameterPanel
	{
		get => _showRightParameterPanel;
		set => this.RaiseAndSetIfChanged(ref _showRightParameterPanel, value);
	}

	private bool _showLeftEventPanel = true;
	/// <summary>
	/// 左側のイベントパネルを表示するか
	/// </summary>
	public bool ShowLeftEventPanel
	{
		get => _showLeftEventPanel;
		set => this.RaiseAndSetIfChanged(ref _showLeftEventPanel, value);
	}

	private bool _showRightEventPanel = true;
	/// <summary>
	/// 右側のイベントパネルを表示するか
	/// </summary>
	public bool ShowRightEventPanel
	{
		get => _showRightEventPanel;
		set => this.RaiseAndSetIfChanged(ref _showRightEventPanel, value);
	}
	#endregion

	#region 検出結果
	public ShakeDetectionVerifierLayer LeftLayer { get; }
	public ShakeDetectionVerifierLayer RightLayer { get; }

	private ShakeDetectionEngine? LeftEngine { get; set; }
	private ShakeDetectionEngine? RightEngine { get; set; }

	private int _leftEventCount;
	public int LeftEventCount
	{
		get => _leftEventCount;
		set => this.RaiseAndSetIfChanged(ref _leftEventCount, value);
	}

	private int _rightEventCount;
	public int RightEventCount
	{
		get => _rightEventCount;
		set => this.RaiseAndSetIfChanged(ref _rightEventCount, value);
	}

	public KyoshinEvent[] LeftEvents => LeftEngine?.KyoshinEvents.ToArray() ?? [];
	public KyoshinEvent[] RightEvents => RightEngine?.KyoshinEvents.ToArray() ?? [];
	#endregion

	#region マップレイヤー
	private MapLayer[]? _leftMapLayers;
	public MapLayer[]? LeftMapLayers
	{
		get => _leftMapLayers;
		private set => this.RaiseAndSetIfChanged(ref _leftMapLayers, value);
	}

	private MapLayer[]? _rightMapLayers;
	public MapLayer[]? RightMapLayers
	{
		get => _rightMapLayers;
		private set => this.RaiseAndSetIfChanged(ref _rightMapLayers, value);
	}
	#endregion

	#region マップ同期
	private double _mapZoom = 6;
	public double MapZoom
	{
		get => _mapZoom;
		set => this.RaiseAndSetIfChanged(ref _mapZoom, value);
	}

	private KyoshinMonitorLib.Location _mapCenterLocation = new(36.474f, 135.264f);
	public KyoshinMonitorLib.Location MapCenterLocation
	{
		get => _mapCenterLocation;
		set => this.RaiseAndSetIfChanged(ref _mapCenterLocation, value);
	}
	#endregion

	private List<(DateTime Time, byte[] ImageData)> ImageFrames { get; } = [];
	private bool _isRecalculating;

	#region 処理中状態
	private bool _isProcessing;
	/// <summary>
	/// 処理中かどうか（UIを無効化するために使用）
	/// </summary>
	public bool IsProcessing
	{
		get => _isProcessing;
		set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
	}

	private double _progressValue;
	/// <summary>
	/// 進捗値（0-100）
	/// </summary>
	public double ProgressValue
	{
		get => _progressValue;
		set => this.RaiseAndSetIfChanged(ref _progressValue, value);
	}

	private bool _isProgressIndeterminate;
	/// <summary>
	/// 進捗が不確定かどうか
	/// </summary>
	public bool IsProgressIndeterminate
	{
		get => _isProgressIndeterminate;
		set => this.RaiseAndSetIfChanged(ref _isProgressIndeterminate, value);
	}

	private CancellationTokenSource? _cancellationTokenSource;

	/// <summary>
	/// 現在の処理をキャンセルする
	/// </summary>
	public void CancelOperation()
	{
		_cancellationTokenSource?.Cancel();
	}
	#endregion

	public ShakeDetectionVerifierSeries(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		ObservationPointsUpdateService observationPointsUpdateService
	) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<ShakeDetectionVerifierSeries>();
		Config = config;
		ObservationPointsUpdateService = observationPointsUpdateService;
		Logger = logManager.GetLogger<ShakeDetectionVerifierSeries>();

		LeftLayer = new ShakeDetectionVerifierLayer(config);
		RightLayer = new ShakeDetectionVerifierLayer(config);
	}

	public override void Initialize()
	{
		MessageBus.Current.Listen<MapLoaded>().Subscribe(x =>
		{
			MapData = x.Data;
			// マップレイヤーの初期化（LandLayer + 検証レイヤー）
			LeftMapLayers = [new LandLayer { Map = MapData }, LeftLayer];
			RightMapLayers = [new LandLayer { Map = MapData }, RightLayer];
		});
	}

	public override void Activating()
	{
		if (_control != null)
			return;

		_control = new ShakeDetectionVerifierView
		{
			DataContext = this
		};
	}

	public override void Deactivated()
	{
	}

	public async Task LoadFileAsync()
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window tlc)
			return;

		var files = await tlc.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = "リプレイファイルを選択",
			AllowMultiple = true,
			FileTypeFilter = [new FilePickerFileType("リプレイファイル") { Patterns = ["*.eqrp"] }]
		});

		if (files.Count == 0)
			return;

		if (files.Count == 1)
			await LoadReplayFileAsync(files[0].Path.LocalPath);
		else
			await LoadReplayFilesAsync(files.Select(f => f.Path.LocalPath).ToArray());
	}

	public async Task LoadDirectoryAsync()
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window tlc)
			return;

		var folders = await tlc.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = "リプレイファイルが含まれるディレクトリを選択",
			AllowMultiple = false
		});

		if (folders.Count == 0)
			return;

		var directory = folders[0].Path.LocalPath;
		var replayFiles = Directory.GetFiles(directory, "*.eqrp");

		if (replayFiles.Length == 0)
		{
			StatusMessage = "ディレクトリ内にリプレイファイルが見つかりません";
			return;
		}

		// すべてのファイルを読み込んで時系列順に並べる
		await LoadReplayFilesAsync(replayFiles);
	}

	private async Task LoadReplayFilesAsync(string[] paths)
	{
		try
		{
			StatusMessage = $"リプレイファイルを読み込み中... (0/{paths.Length})";

			ImageFrames.Clear();
			LoadedHeader = null;
			LoadedData = null;

			var allImageFrames = new List<(DateTime Time, byte[] ImageData)>();

			for (var fileIndex = 0; fileIndex < paths.Length; fileIndex++)
			{
				var path = paths[fileIndex];
				StatusMessage = $"リプレイファイルを読み込み中... ({fileIndex + 1}/{paths.Length})";

				try
				{
					using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
					var reader = new KyoshinReplayFileReader(stream);
					var header = await reader.ReadHeader();
					var data = await reader.ReadData(header.CompressionMode);

					// 最初のファイルのヘッダーを保持
					LoadedHeader ??= header;

					// 画像フレームを抽出
					foreach (var frameData in data)
					{
						if (frameData is KyoshinMonitorImageReplayData imgData &&
							imgData.Images.TryGetValue(KyoshinMonitorImageReplayData.ImageType.Shindo, out var imageBytes))
						{
							allImageFrames.Add((frameData.Time, imageBytes));
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, $"リプレイファイルの読み込みに失敗しました: {path}");
				}
			}

			if (allImageFrames.Count == 0)
			{
				StatusMessage = "リプレイファイルに画像データが含まれていません";
				IsDataLoaded = false;
				return;
			}

			// 時系列順にソート
			allImageFrames.Sort((a, b) => a.Time.CompareTo(b.Time));

			// ソート結果をImageFramesに追加
			ImageFrames.AddRange(allImageFrames);

			MaxFrameIndex = ImageFrames.Count - 1;
			CurrentFrameIndex = 0;
			IsDataLoaded = true;

			// エンジンを初期化
			await InitializeEnginesAsync();

			StatusMessage = $"読み込み完了: {paths.Length} ファイル、{ImageFrames.Count} フレーム";
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "リプレイファイルの読み込みに失敗しました");
			StatusMessage = $"読み込みエラー: {ex.Message}";
			IsDataLoaded = false;
		}
	}

	private async Task LoadReplayFileAsync(string path)
	{
		try
		{
			StatusMessage = "リプレイファイルを読み込み中...";

			using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var reader = new KyoshinReplayFileReader(stream);
			LoadedHeader = await reader.ReadHeader();
			LoadedData = await reader.ReadData(LoadedHeader.CompressionMode);

			// 画像フレームを抽出
			ImageFrames.Clear();
			foreach (var data in LoadedData)
			{
				if (data is KyoshinMonitorImageReplayData imgData &&
					imgData.Images.TryGetValue(KyoshinMonitorImageReplayData.ImageType.Shindo, out var imageBytes))
				{
					ImageFrames.Add((data.Time, imageBytes));
				}
			}

			if (ImageFrames.Count == 0)
			{
				StatusMessage = "リプレイファイルに画像データが含まれていません";
				IsDataLoaded = false;
				return;
			}

			MaxFrameIndex = ImageFrames.Count - 1;
			CurrentFrameIndex = 0;
			IsDataLoaded = true;

			// エンジンを初期化
			await InitializeEnginesAsync();

			StatusMessage = $"読み込み完了: {ImageFrames.Count} フレーム";
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "リプレイファイルの読み込みに失敗しました");
			StatusMessage = $"読み込みエラー: {ex.Message}";
			IsDataLoaded = false;
		}
	}

	private async Task InitializeEnginesAsync()
	{
		var observationPoints = await ObservationPointsUpdateService.GetObservationPointsAsync();

		// 左右のエンジンを初期化
		LeftEngine = new ShakeDetectionEngine(null, LeftParameters);
		RightEngine = new ShakeDetectionEngine(null, RightParameters);

		// 観測点を作成してエンジンに設定
		var leftPoints = CreateObservationPoints(observationPoints);
		var rightPoints = CreateObservationPoints(observationPoints);

		LeftEngine.Initialize(leftPoints);
		RightEngine.Initialize(rightPoints);
	}

	private static RealtimeObservationPoint[] CreateObservationPoints(Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2[] sourcePoints)
	{
		return sourcePoints
			.Where(p => p is { Point: not null, IsSuspended: false })
			.Select(p => new RealtimeObservationPoint(p))
			.ToArray();
	}

	private void UpdateCurrentTime()
	{
		if (!IsDataLoaded || ImageFrames.Count == 0)
			return;

		if (CurrentFrameIndex >= 0 && CurrentFrameIndex < ImageFrames.Count)
		{
			CurrentTime = ImageFrames[CurrentFrameIndex].Time;
			CurrentTimeText = CurrentTime.ToString("HH:mm:ss");
			ProcessCurrentFrame();
		}
	}

	private void ProcessCurrentFrame()
	{
		if (_isRecalculating || !IsDataLoaded || LeftEngine == null || RightEngine == null)
			return;

		if (CurrentFrameIndex < 0 || CurrentFrameIndex >= ImageFrames.Count)
			return;

		var (time, imageData) = ImageFrames[CurrentFrameIndex];

		using var bitmap = SKBitmap.Decode(imageData);
		if (bitmap == null)
			return;

		// 左右のエンジンで並列処理
		Parallel.Invoke(
			() => ProcessImageWithEngine(bitmap, time, LeftEngine, LeftLayer),
			() => ProcessImageWithEngine(bitmap, time, RightEngine, RightLayer)
		);

		LeftEventCount = LeftEngine.KyoshinEvents.Count;
		RightEventCount = RightEngine.KyoshinEvents.Count;
		this.RaisePropertyChanged(nameof(LeftEvents));
		this.RaisePropertyChanged(nameof(RightEvents));
	}

	private static void ProcessImageWithEngine(SKBitmap bitmap, DateTime time, ShakeDetectionEngine engine, ShakeDetectionVerifierLayer layer)
	{
		// エンジンで画像を処理（イベント検出含む）
		engine.ProcessImage(bitmap, time);

		// レイヤーを更新
		layer.CurrentTime = time;
		layer.ObservationPoints = engine.Points;
		layer.KyoshinEvents = engine.KyoshinEvents.ToArray();
	}

	public void PreviousFrame()
	{
		if (CurrentFrameIndex > 0)
			CurrentFrameIndex--;
	}

	public void NextFrame()
	{
		if (CurrentFrameIndex < MaxFrameIndex)
			CurrentFrameIndex++;
	}

	/// <summary>
	/// IsConfirmed=true のイベントが発生するまで進む
	/// </summary>
	public async Task SeekToNextEventAsync()
	{
		if (!IsDataLoaded || LeftEngine == null || RightEngine == null || IsProcessing)
			return;

		IsProcessing = true;
		IsProgressIndeterminate = true;
		_cancellationTokenSource = new CancellationTokenSource();
		var token = _cancellationTokenSource.Token;

		var confirmedEventIds = GetConfirmedEventIds();
		StatusMessage = "確定イベントを検索中...";

		try
		{
			var stopReason = "";
			await Task.Run(() =>
			{
				while (CurrentFrameIndex < MaxFrameIndex)
				{
					if (token.IsCancellationRequested)
						break;
					CurrentFrameIndex++;

					// IsConfirmed が false から true に変化したイベントがあるか確認
					var newlyConfirmedEvent = FindNewlyConfirmedEvent(confirmedEventIds);
					if (newlyConfirmedEvent != null)
					{
						stopReason = $"イベント確定: {newlyConfirmedEvent.PointCount} 点";
						break;
					}

					confirmedEventIds = GetConfirmedEventIds();
				}
			}, token);

			if (token.IsCancellationRequested)
			{
				StatusMessage = "検索をキャンセルしました";
			}
			else if (CurrentFrameIndex >= MaxFrameIndex && string.IsNullOrEmpty(stopReason))
			{
				StatusMessage = "最後のフレームに到達しました";
			}
			else
			{
				StatusMessage = stopReason;
			}
		}
		finally
		{
			IsProcessing = false;
			IsProgressIndeterminate = false;
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
		}
	}

	/// <summary>
	/// 現在の IsConfirmed が true のイベントの ID を取得
	/// </summary>
	private HashSet<Guid> GetConfirmedEventIds()
	{
		var ids = new HashSet<Guid>();
		if (LeftEngine != null)
		{
			foreach (var evt in LeftEngine.KyoshinEvents.Where(e => e.IsConfirmed))
				ids.Add(evt.Id);
		}
		if (RightEngine != null)
		{
			foreach (var evt in RightEngine.KyoshinEvents.Where(e => e.IsConfirmed))
				ids.Add(evt.Id);
		}
		return ids;
	}

	/// <summary>
	/// 新たに IsConfirmed が true になったイベントを検索
	/// </summary>
	private KyoshinEvent? FindNewlyConfirmedEvent(HashSet<Guid> previousConfirmedIds)
	{
		if (LeftEngine != null)
		{
			var newlyConfirmed = LeftEngine.KyoshinEvents.FirstOrDefault(e => e.IsConfirmed && !previousConfirmedIds.Contains(e.Id));
			if (newlyConfirmed != null)
				return newlyConfirmed;
		}
		if (RightEngine != null)
		{
			var newlyConfirmed = RightEngine.KyoshinEvents.FirstOrDefault(e => e.IsConfirmed && !previousConfirmedIds.Contains(e.Id));
			if (newlyConfirmed != null)
				return newlyConfirmed;
		}
		return null;
	}

	/// <summary>
	/// 左右のイベントに差異が発生するまで進む（イベント数および各イベントの観測点数を比較）
	/// </summary>
	public async Task SeekToDifferenceAsync()
	{
		if (!IsDataLoaded || LeftEngine == null || RightEngine == null || IsProcessing)
			return;

		IsProcessing = true;
		IsProgressIndeterminate = true;
		_cancellationTokenSource = new CancellationTokenSource();
		var token = _cancellationTokenSource.Token;

		StatusMessage = "左右の差異を検索中...";

		try
		{
			await Task.Run(() =>
			{
				while (CurrentFrameIndex < MaxFrameIndex)
				{
					if (token.IsCancellationRequested)
						break;
					CurrentFrameIndex++;
					if (HasEventDifference())
						break;
				}
			}, token);

			if (token.IsCancellationRequested)
			{
				StatusMessage = "検索をキャンセルしました";
			}
			else if (CurrentFrameIndex >= MaxFrameIndex)
			{
				StatusMessage = HasEventDifference()
					? GetDifferenceMessage()
					: "差異なしで最後のフレームに到達しました";
			}
			else
			{
				StatusMessage = GetDifferenceMessage();
			}
		}
		finally
		{
			IsProcessing = false;
			IsProgressIndeterminate = false;
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
		}
	}

	/// <summary>
	/// 左右のイベントに差異があるかどうかを判定
	/// </summary>
	private bool HasEventDifference()
	{
		if (LeftEngine == null || RightEngine == null)
			return false;

		// イベント数が異なる場合
		if (LeftEngine.KyoshinEvents.Count != RightEngine.KyoshinEvents.Count)
			return true;

		// 各イベントの観測点数を比較
		var leftPointCounts = LeftEngine.KyoshinEvents.Select(e => e.PointCount).OrderBy(c => c).ToArray();
		var rightPointCounts = RightEngine.KyoshinEvents.Select(e => e.PointCount).OrderBy(c => c).ToArray();

		return !leftPointCounts.SequenceEqual(rightPointCounts);
	}

	/// <summary>
	/// 差異の詳細メッセージを取得
	/// </summary>
	private string GetDifferenceMessage()
	{
		if (LeftEngine == null || RightEngine == null)
			return "エンジンが初期化されていません";

		var leftCount = LeftEngine.KyoshinEvents.Count;
		var rightCount = RightEngine.KyoshinEvents.Count;

		if (leftCount != rightCount)
			return $"差異発生: イベント数 左{leftCount} 件 / 右{rightCount} 件";

		var leftPoints = LeftEngine.KyoshinEvents.Sum(e => e.PointCount);
		var rightPoints = RightEngine.KyoshinEvents.Sum(e => e.PointCount);
		return $"差異発生: 観測点数 左{leftPoints} / 右{rightPoints}";
	}

	/// <summary>
	/// 再計算時に遡るフレーム数の閾値
	/// </summary>
	private const int RecalculateFrameThreshold = 300;

	public Task RecalculateLeftAsync() => RecalculateAsync();

	public Task RecalculateRightAsync() => RecalculateAsync();

	/// <summary>
	/// 左右両方のパラメータで再計算を行う
	/// 現在位置が300フレーム以上進んでいる場合は300フレーム前から再計算を開始する
	/// </summary>
	private async Task RecalculateAsync()
	{
		if (!IsDataLoaded || IsProcessing)
			return;

		IsProcessing = true;
		_isRecalculating = true;
		ProgressValue = 0;
		IsProgressIndeterminate = false;
		_cancellationTokenSource = new CancellationTokenSource();
		var token = _cancellationTokenSource.Token;

		StatusMessage = "左右両方を再計算中...";

		try
		{
			var observationPoints = await ObservationPointsUpdateService.GetObservationPointsAsync();
			var targetFrameIndex = CurrentFrameIndex;

			// 300フレーム以上進んでいる場合は300フレーム前から再計算
			var startFrameIndex = targetFrameIndex >= RecalculateFrameThreshold
				? targetFrameIndex - RecalculateFrameThreshold
				: 0;
			var totalFrames = targetFrameIndex - startFrameIndex + 1;

			// 左右両方のエンジンを初期化
			var leftEngine = new ShakeDetectionEngine(null, LeftParameters);
			var rightEngine = new ShakeDetectionEngine(null, RightParameters);

			var leftPoints = CreateObservationPoints(observationPoints);
			var rightPoints = CreateObservationPoints(observationPoints);

			leftEngine.Initialize(leftPoints);
			rightEngine.Initialize(rightPoints);

			LeftEngine = leftEngine;
			RightEngine = rightEngine;

			await Task.Run(() =>
			{
				// 開始位置から現在のフレームまで再処理
				for (var i = startFrameIndex; i <= targetFrameIndex && i < ImageFrames.Count; i++)
				{
					if (token.IsCancellationRequested)
						break;

					var (time, imageData) = ImageFrames[i];
					using var bitmap = SKBitmap.Decode(imageData);
					if (bitmap != null)
					{
						// 左右を並列処理
						Parallel.Invoke(
							() => leftEngine.ProcessImage(bitmap, time),
							() => rightEngine.ProcessImage(bitmap, time)
						);
					}

					ProgressValue = (double)(i - startFrameIndex + 1) / totalFrames * 100;
				}
			}, token);

			if (token.IsCancellationRequested)
			{
				StatusMessage = "再計算をキャンセルしました";
			}
			else
			{
				LeftLayer.ObservationPoints = LeftEngine.Points;
				LeftLayer.KyoshinEvents = LeftEngine.KyoshinEvents.ToArray();
				LeftEventCount = LeftEngine.KyoshinEvents.Count;
				this.RaisePropertyChanged(nameof(LeftEvents));

				RightLayer.ObservationPoints = RightEngine.Points;
				RightLayer.KyoshinEvents = RightEngine.KyoshinEvents.ToArray();
				RightEventCount = RightEngine.KyoshinEvents.Count;
				this.RaisePropertyChanged(nameof(RightEvents));

				var frameInfo = startFrameIndex > 0
					? $"（{startFrameIndex} フレーム目から {totalFrames} フレーム処理）"
					: $"（全 {totalFrames} フレーム処理）";
				StatusMessage = $"左右両方の再計算が完了しました {frameInfo}";
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "再計算に失敗しました");
			StatusMessage = $"再計算エラー: {ex.Message}";
		}
		finally
		{
			IsProcessing = false;
			_isRecalculating = false;
			ProgressValue = 0;
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
		}
	}
}
