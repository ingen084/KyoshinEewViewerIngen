using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.Ml.Layers;
using KyoshinEewViewer.Series.Ml.Services;
using KyoshinMonitorLib.SkiaImages;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Ml;

public class MlSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(MlSeries), "ml", "[実験]データセット作成", new FontIconSource { Glyph = "\xf085", FontFamily = new FontFamily(Utils.IconFontName) }, true, "機械学習用データセット作成ツール");

	private MlView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [];

	/// <summary>
	/// データセット用レイヤー
	/// </summary>
	public MlDatasetLayer DatasetLayer { get; }

	/// <summary>
	/// 観測点更新サービス
	/// </summary>
	private ObservationPointsUpdateService? ObservationPointsUpdateService { get; set; }

	/// <summary>
	/// 震度DBサービス
	/// </summary>
	private ShindoDbService ShindoDbService { get; } = new();

	/// <summary>
	/// 読み込んだ観測点
	/// </summary>
	private RealtimeObservationPoint[]? _observationPoints;
	public RealtimeObservationPoint[]? ObservationPoints
	{
		get => _observationPoints;
		private set
		{
			this.RaiseAndSetIfChanged(ref _observationPoints, value);
			DatasetLayer.ObservationPoints = value;
		}
	}

	/// <summary>
	/// 読み込んだリプレイデータ
	/// </summary>
	private KyoshinMonitorImageReplayData[]? _replayData;
	public KyoshinMonitorImageReplayData[]? ReplayData
	{
		get => _replayData;
		private set => this.RaiseAndSetIfChanged(ref _replayData, value);
	}

	/// <summary>
	/// 読み込んだリプレイファイルのヘッダ
	/// </summary>
	private ReplayFileHeader? _replayHeader;
	public ReplayFileHeader? ReplayHeader
	{
		get => _replayHeader;
		private set => this.RaiseAndSetIfChanged(ref _replayHeader, value);
	}

	/// <summary>
	/// 震度DBから取得した地震情報
	/// </summary>
	private ShindoDbEarthquake[]? _earthquakes;
	public ShindoDbEarthquake[]? Earthquakes
	{
		get => _earthquakes;
		private set => this.RaiseAndSetIfChanged(ref _earthquakes, value);
	}

	/// <summary>
	/// 読み込み中かどうか
	/// </summary>
	private bool _isLoading;
	public bool IsLoading
	{
		get => _isLoading;
		private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
	}

	/// <summary>
	/// ステータスメッセージ
	/// </summary>
	private string _statusMessage = "データを読み込んでください";
	public string StatusMessage
	{
		get => _statusMessage;
		private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
	}

	/// <summary>
	/// 1時間ごとにグループ化されたデータのインデックス
	/// </summary>
	private List<(DateTime HourStart, int StartIndex, int EndIndex)> _hourlyGroups = [];

	/// <summary>
	/// 時間スライダーの最大値（時間単位）
	/// </summary>
	private int _hourSliderMaximum;
	public int HourSliderMaximum
	{
		get => _hourSliderMaximum;
		private set => this.RaiseAndSetIfChanged(ref _hourSliderMaximum, value);
	}

	/// <summary>
	/// 時間スライダーの現在値（時間単位のインデックス）
	/// </summary>
	private int _hourSliderValue;
	public int HourSliderValue
	{
		get => _hourSliderValue;
		set
		{
			if (_hourSliderValue == value)
				return;
			this.RaiseAndSetIfChanged(ref _hourSliderValue, value);
			UpdateSecondSliderRange();
			// 秒スライダーを先頭に設定
			SecondSliderValue = 0;
		}
	}

	/// <summary>
	/// 現在選択中の時間帯の表示
	/// </summary>
	private string _currentHourDisplay = "";
	public string CurrentHourDisplay
	{
		get => _currentHourDisplay;
		private set => this.RaiseAndSetIfChanged(ref _currentHourDisplay, value);
	}

	/// <summary>
	/// 秒スライダーの最大値
	/// </summary>
	private int _secondSliderMaximum;
	public int SecondSliderMaximum
	{
		get => _secondSliderMaximum;
		private set => this.RaiseAndSetIfChanged(ref _secondSliderMaximum, value);
	}

	/// <summary>
	/// 秒スライダーの現在値（時間内の相対インデックス）
	/// </summary>
	private int _secondSliderValue;
	public int SecondSliderValue
	{
		get => _secondSliderValue;
		set
		{
			this.RaiseAndSetIfChanged(ref _secondSliderValue, value);
			UpdateDisplayForCurrentTime();
		}
	}

	/// <summary>
	/// 現在表示中の時刻
	/// </summary>
	private DateTime _currentDisplayTime;
	public DateTime CurrentDisplayTime
	{
		get => _currentDisplayTime;
		private set => this.RaiseAndSetIfChanged(ref _currentDisplayTime, value);
	}

	/// <summary>
	/// データが読み込まれているか
	/// </summary>
	private bool _hasData;
	public bool HasData
	{
		get => _hasData;
		private set => this.RaiseAndSetIfChanged(ref _hasData, value);
	}

	/// <summary>
	/// 現在表示中の地震数
	/// </summary>
	private int _activeEarthquakeCount;
	public int ActiveEarthquakeCount
	{
		get => _activeEarthquakeCount;
		private set => this.RaiseAndSetIfChanged(ref _activeEarthquakeCount, value);
	}

	/// <summary>
	/// 全地震数
	/// </summary>
	private int _totalEarthquakeCount;
	public int TotalEarthquakeCount
	{
		get => _totalEarthquakeCount;
		private set => this.RaiseAndSetIfChanged(ref _totalEarthquakeCount, value);
	}

	/// <summary>
	/// エクスポート中かどうか
	/// </summary>
	private bool _isExporting;
	public bool IsExporting
	{
		get => _isExporting;
		private set => this.RaiseAndSetIfChanged(ref _isExporting, value);
	}

	/// <summary>
	/// エクスポート進捗（0-100）
	/// </summary>
	private double _exportProgress;
	public double ExportProgress
	{
		get => _exportProgress;
		private set => this.RaiseAndSetIfChanged(ref _exportProgress, value);
	}

	/// <summary>
	/// エクスポートステータスメッセージ
	/// </summary>
	private string _exportStatusMessage = "";
	public string ExportStatusMessage
	{
		get => _exportStatusMessage;
		private set => this.RaiseAndSetIfChanged(ref _exportStatusMessage, value);
	}

	public MlSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<MlSeries>();

		DatasetLayer = new MlDatasetLayer();
	}

	public override void Activating()
	{
		if (_control != null)
			return;

		_control = new MlView
		{
			DataContext = this,
		};

		MapDisplayParameter = new()
		{
			OverlayLayers = [DatasetLayer],
		};
	}

	public override void Deactivated() { }

	/// <summary>
	/// eqrpファイルを開く
	/// </summary>
	public async void OpenReplayFile()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "eqrpファイルを開く",
				FileTypeFilter = [
					new("地震リプレイファイル") { Patterns = ["*.eqrp"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				await LoadReplayFileAsync(filePath);
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル読み込みエラー", $"ファイルの読み込みに失敗しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// ディレクトリ内のeqrpファイルを開く
	/// </summary>
	public async void OpenReplayDirectory()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

			var folders = await tlc.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
			{
				Title = "eqrpファイルが格納されているディレクトリを選択",
				AllowMultiple = false,
			});

			if (folders?.Count > 0)
			{
				var dirPath = folders[0].Path.LocalPath;
				var eqrpFiles = Directory.GetFiles(dirPath, "*.eqrp");

				if (eqrpFiles.Length == 0)
				{
					await ShowErrorDialog("ファイルが見つかりません", "選択したディレクトリにeqrpファイルがありません。");
					return;
				}

				// 複数ファイルを統合して読み込む
				await LoadMultipleReplayFilesAsync(eqrpFiles);
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ディレクトリ読み込みエラー", $"ディレクトリの読み込みに失敗しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// 単一のリプレイファイルを読み込む
	/// </summary>
	private async Task LoadReplayFileAsync(string filePath)
	{
		IsLoading = true;
		StatusMessage = "ファイルを読み込み中...";

		try
		{
			// 観測点を初期化
			await InitializeObservationPointsAsync();

			// リプレイファイルを読み込み
			using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new KyoshinReplayFileReader(stream);
			ReplayHeader = await reader.ReadHeader();
			var allData = await reader.ReadData(ReplayHeader.CompressionMode);

			// 強震モニタ画像データのみを抽出
			ReplayData = allData
				.OfType<KyoshinMonitorImageReplayData>()
				.OrderBy(d => d.Time)
				.ToArray();

			if (ReplayData.Length == 0)
			{
				StatusMessage = "強震モニタデータが含まれていません";
				HasData = false;
				return;
			}

				// 時間グループを構築してスライダーを初期化
			BuildHourlyGroups();

			// 震度DBから地震情報を取得
			await FetchEarthquakesFromShindoDbAsync(ReplayHeader.StartTime, ReplayHeader.EndTime);

			HasData = true;
			StatusMessage = $"読み込み完了: {ReplayData.Length}件のデータ、{TotalEarthquakeCount}件の地震";
		}
		catch (Exception ex)
		{
			StatusMessage = $"読み込みエラー: {ex.Message}";
			HasData = false;
			throw;
		}
		finally
		{
			IsLoading = false;
		}
	}

	/// <summary>
	/// 複数のリプレイファイルを読み込む
	/// </summary>
	private async Task LoadMultipleReplayFilesAsync(string[] filePaths)
	{
		IsLoading = true;
		StatusMessage = $"ファイルを読み込み中... (0/{filePaths.Length})";

		try
		{
			// 観測点を初期化
			await InitializeObservationPointsAsync();

			var allImageData = new List<KyoshinMonitorImageReplayData>();
			DateTime? minTime = null;
			DateTime? maxTime = null;

			for (var i = 0; i < filePaths.Length; i++)
			{
				StatusMessage = $"ファイルを読み込み中... ({i + 1}/{filePaths.Length})";

				using var stream = File.Open(filePaths[i], FileMode.Open, FileAccess.Read, FileShare.Read);
				using var reader = new KyoshinReplayFileReader(stream);
				var header = await reader.ReadHeader();
				var data = await reader.ReadData(header.CompressionMode);

				var imageData = data.OfType<KyoshinMonitorImageReplayData>().ToArray();
				allImageData.AddRange(imageData);

				if (minTime == null || header.StartTime < minTime)
					minTime = header.StartTime;
				if (maxTime == null || header.EndTime > maxTime)
					maxTime = header.EndTime;

				// 最初のファイルのヘッダを保持
				ReplayHeader ??= header;
			}

			// 時刻順にソートして重複を除去
			ReplayData = allImageData
				.GroupBy(d => d.Time)
				.Select(g => g.First())
				.OrderBy(d => d.Time)
				.ToArray();

			if (ReplayData.Length == 0)
			{
				StatusMessage = "強震モニタデータが含まれていません";
				HasData = false;
				return;
			}

			// ヘッダの時刻範囲を更新
			if (ReplayHeader != null && minTime.HasValue && maxTime.HasValue)
			{
				ReplayHeader = new ReplayFileHeader
				{
					Version = ReplayHeader.Version,
					SoftwareName = ReplayHeader.SoftwareName,
					StartTime = minTime.Value,
					EndTime = maxTime.Value,
					CompressionMode = ReplayHeader.CompressionMode,
				};
			}

				// 時間グループを構築してスライダーを初期化
			BuildHourlyGroups();

			// 震度DBから地震情報を取得
			if (minTime.HasValue && maxTime.HasValue)
				await FetchEarthquakesFromShindoDbAsync(minTime.Value, maxTime.Value);

			HasData = true;
			StatusMessage = $"読み込み完了: {filePaths.Length}ファイル、{ReplayData.Length}件のデータ、{TotalEarthquakeCount}件の地震";
		}
		catch (Exception ex)
		{
			StatusMessage = $"読み込みエラー: {ex.Message}";
			HasData = false;
			throw;
		}
		finally
		{
			IsLoading = false;
		}
	}

	/// <summary>
	/// 観測点を初期化する
	/// </summary>
	private async Task InitializeObservationPointsAsync()
	{
		// 走時表を初期化
		if (!TravelTimeTableService.IsInitialized)
			TravelTimeTableService.Initialize();

		// 観測点を取得
		ObservationPointsUpdateService ??= Locator.Current.GetService<ObservationPointsUpdateService>();
		if (ObservationPointsUpdateService == null)
			throw new InvalidOperationException("ObservationPointsUpdateServiceが見つかりません");

		var observationPointsV2 = await ObservationPointsUpdateService.GetObservationPointsAsync();
		ObservationPoints = observationPointsV2
			.Where(p => p is { Point: not null, IsSuspended: false })
			.Select(p => new RealtimeObservationPoint(p))
			.ToArray();
	}

	/// <summary>
	/// 震度DBから地震情報を取得する
	/// </summary>
	private async Task FetchEarthquakesFromShindoDbAsync(DateTime from, DateTime to)
	{
		StatusMessage = "震度DBから地震情報を取得中...";

		Earthquakes = await ShindoDbService.SearchAsync(from, to);
		TotalEarthquakeCount = Earthquakes.Length;
	}

	/// <summary>
	/// 1時間ごとのグループを構築する
	/// </summary>
	private void BuildHourlyGroups()
	{
		if (ReplayData == null || ReplayData.Length == 0)
			return;

		_hourlyGroups.Clear();

		var currentHour = new DateTime(ReplayData[0].Time.Year, ReplayData[0].Time.Month, ReplayData[0].Time.Day, ReplayData[0].Time.Hour, 0, 0);
		var startIndex = 0;

		for (var i = 0; i < ReplayData.Length; i++)
		{
			var dataHour = new DateTime(ReplayData[i].Time.Year, ReplayData[i].Time.Month, ReplayData[i].Time.Day, ReplayData[i].Time.Hour, 0, 0);
			if (dataHour != currentHour)
			{
				_hourlyGroups.Add((currentHour, startIndex, i - 1));
				currentHour = dataHour;
				startIndex = i;
			}
		}
		// 最後のグループを追加
		_hourlyGroups.Add((currentHour, startIndex, ReplayData.Length - 1));

		// スライダーの範囲を設定
		HourSliderMaximum = Math.Max(0, _hourlyGroups.Count - 1);
		_hourSliderValue = 0;
		this.RaisePropertyChanged(nameof(HourSliderValue));
		UpdateSecondSliderRange();
		SecondSliderValue = 0;
	}

	/// <summary>
	/// 秒スライダーの範囲を更新する
	/// </summary>
	private void UpdateSecondSliderRange()
	{
		if (_hourlyGroups.Count == 0 || HourSliderValue < 0 || HourSliderValue >= _hourlyGroups.Count)
		{
			SecondSliderMaximum = 0;
			CurrentHourDisplay = "";
			return;
		}

		var group = _hourlyGroups[HourSliderValue];
		SecondSliderMaximum = group.EndIndex - group.StartIndex;
		CurrentHourDisplay = group.HourStart.ToString("yyyy/MM/dd HH:00");
	}

	/// <summary>
	/// 現在のスライダー位置から実際のデータインデックスを取得する
	/// </summary>
	private int GetCurrentDataIndex()
	{
		if (_hourlyGroups.Count == 0 || HourSliderValue < 0 || HourSliderValue >= _hourlyGroups.Count)
			return 0;

		var group = _hourlyGroups[HourSliderValue];
		return Math.Clamp(group.StartIndex + SecondSliderValue, group.StartIndex, group.EndIndex);
	}

	/// <summary>
	/// 現在時刻の表示を更新する
	/// </summary>
	private void UpdateDisplayForCurrentTime()
	{
		if (ReplayData == null || ReplayData.Length == 0 || ObservationPoints == null)
			return;

		var index = GetCurrentDataIndex();
		var data = ReplayData[index];
		CurrentDisplayTime = data.Time;
		DatasetLayer.CurrentTime = data.Time;

		// 強震モニタ画像をパースして観測点の色を更新
		if (data.Images.TryGetValue(KyoshinMonitorImageReplayData.ImageType.Shindo, out var imageBytes))
		{
			using var bitmap = SKBitmap.Decode(imageBytes);
			if (bitmap != null)
				UpdateObservationPointsFromImage(bitmap);
		}

		// 有効な地震情報を更新
		if (Earthquakes != null)
		{
			var activeEqs = ShindoDbService.GetActiveEarthquakes(Earthquakes, data.Time);
			DatasetLayer.ActiveEarthquakes = activeEqs;
			ActiveEarthquakeCount = activeEqs.Length;
		}
	}

	/// <summary>
	/// 画像から観測点の色と震度を更新する
	/// </summary>
	private void UpdateObservationPointsFromImage(SKBitmap bitmap)
	{
		if (ObservationPoints == null)
			return;

		foreach (var point in ObservationPoints)
		{
			var color = bitmap.GetPixel(point.ImageLocation.X, point.ImageLocation.Y);
			if (color.Alpha != 255)
			{
				point.Update(null, null);
				continue;
			}
			var intensity = ColorConverter.ConvertToIntensityFromScale(
				ColorConverter.ConvertToScaleAtPolynomialInterpolation(color));
			point.Update(color, (float)intensity);
		}

		// レイヤーに変更を通知
		DatasetLayer.ObservationPoints = ObservationPoints;
	}

	/// <summary>
	/// エラーダイアログを表示する
	/// </summary>
	private static async Task ShowErrorDialog(string title, string message)
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

		await new ContentDialog
		{
			Title = title,
			Content = message,
			CloseButtonText = "OK"
		}.ShowAsync(tlc);
	}

	/// <summary>
	/// データセットをエクスポートする
	/// </summary>
	public async void ExportDataset()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

			if (ReplayData == null || ObservationPoints == null)
			{
				await ShowErrorDialog("エクスポートエラー", "データが読み込まれていません。");
				return;
			}

			// 出力先ディレクトリを選択
			var folders = await tlc.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
			{
				Title = "出力先ディレクトリを選択",
				AllowMultiple = false,
			});

			if (folders?.Count == 0) return;

			var outputDir = folders![0].Path.LocalPath;

			// タイムスタンプ付きのサブディレクトリを作成
			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var exportDir = Path.Combine(outputDir, $"dataset_{timestamp}");

			IsExporting = true;
			ExportProgress = 0;
			ExportStatusMessage = "エクスポートを開始...";

			var exportService = new DatasetExportService();
			var progress = new Progress<DatasetExportService.ExportProgress>(p =>
			{
				ExportProgress = p.Percentage;
				ExportStatusMessage = p.Message;
			});

			await Task.Run(async () =>
			{
				await exportService.ExportAsync(
					exportDir,
					ReplayData,
					ObservationPoints,
					Earthquakes ?? [],
					progress);
			});

			ExportStatusMessage = $"エクスポート完了: {exportDir}";

			await new ContentDialog
			{
				Title = "エクスポート完了",
				Content = $"データセットを出力しました。\n\n出力先: {exportDir}\n\nPythonスクリプトで変換してください。",
				CloseButtonText = "OK"
			}.ShowAsync(tlc);
		}
		catch (OperationCanceledException)
		{
			ExportStatusMessage = "エクスポートがキャンセルされました";
		}
		catch (Exception ex)
		{
			ExportStatusMessage = $"エクスポートエラー: {ex.Message}";
			await ShowErrorDialog("エクスポートエラー", $"エクスポートに失敗しました。\n\n{ex.Message}");
		}
		finally
		{
			IsExporting = false;
		}
	}
}
