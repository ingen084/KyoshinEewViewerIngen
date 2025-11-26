using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Series.Ml.Layers;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Ml;

public class MlSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(MlSeries), "ml", "[実験]機械学習", new FontIconSource { Glyph = "\xf085", FontFamily = new FontFamily(Utils.IconFontName) }, true, "機械学習実験用シリーズ");

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	private MlView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [];

	/// <summary>
	/// グリッドレイヤー
	/// </summary>
	public MlGridLayer GridLayer { get; }

	/// <summary>
	/// 観測点レイヤー
	/// </summary>
	public MlObservationPointLayer ObservationPointLayer { get; }

	/// <summary>
	/// 読み込んだ観測点
	/// </summary>
	private CommonObservationPoint[] _observationPoints = [];
	public CommonObservationPoint[] ObservationPoints
	{
		get => _observationPoints;
		private set => this.RaiseAndSetIfChanged(ref _observationPoints, value);
	}

	/// <summary>
	/// 強震モニタ座標が設定されている観測点数
	/// </summary>
	private int _validObservationPointCount;
	public int ValidObservationPointCount
	{
		get => _validObservationPointCount;
		private set => this.RaiseAndSetIfChanged(ref _validObservationPointCount, value);
	}

	/// <summary>
	/// グリッド分割単位（度）
	/// </summary>
	private double _gridUnit = 0.1;
	public double GridUnit
	{
		get => _gridUnit;
		set => this.RaiseAndSetIfChanged(ref _gridUnit, value);
	}

	/// <summary>
	/// グリッド数
	/// </summary>
	private int _gridCount;
	public int GridCount
	{
		get => _gridCount;
		private set => this.RaiseAndSetIfChanged(ref _gridCount, value);
	}

	public MlSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<MlSeries>();

		GridLayer = new MlGridLayer();
		ObservationPointLayer = new MlObservationPointLayer();

		// グリッド単位が変更されたらグリッドを再計算
		this.WhenAnyValue(x => x.GridUnit)
			.Throttle(TimeSpan.FromMilliseconds(300))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => UpdateGrid());
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
			OverlayLayers = [GridLayer, ObservationPointLayer],
		};
	}

	public override void Deactivated() { }

	/// <summary>
	/// 観測点ファイルを開く
	/// </summary>
	public async void OpenObservationPointFile()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "観測点JSONファイルを開く",
				FileTypeFilter = [
					new("JSON") { Patterns = ["*.json"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				await LoadObservationPointsFromFile(filePath);
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル読み込みエラー", $"ファイルの読み込みに失敗しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// 観測点ファイルを読み込む
	/// </summary>
	private async Task LoadObservationPointsFromFile(string filePath)
	{
		try
		{
			var points = await JsonSerializer.DeserializeAsync<CommonObservationPoint[]>(File.OpenRead(filePath), JsonOptions)
				?? throw new InvalidOperationException("JSONファイルの読み込みに失敗しました。");

			ObservationPoints = points;

			// 強震モニタ座標が設定されている観測点をカウント
			ValidObservationPointCount = points.Count(p => p.Point != null);

			// 観測点レイヤーを更新
			ObservationPointLayer.UpdateObservationPoints(points);

			// グリッドを更新
			UpdateGrid();
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル読み込みエラー", $"ファイルの読み込み中にエラーが発生しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// グリッドを更新する
	/// </summary>
	private void UpdateGrid()
	{
		// 強震モニタ座標が設定されている観測点のみを対象とする
		var validPoints = ObservationPoints.Where(p => p.Point != null && p.Location != null).ToArray();

		if (validPoints.Length == 0)
		{
			GridLayer.UpdateGridCells([]);
			GridCount = 0;
			return;
		}

		// 観測点の緯度経度の範囲を取得
		var minLat = validPoints.Min(p => p.Location.Latitude);
		var maxLat = validPoints.Max(p => p.Location.Latitude);
		var minLng = validPoints.Min(p => p.Location.Longitude);
		var maxLng = validPoints.Max(p => p.Location.Longitude);

		// グリッドを生成
		var gridCells = new List<GridCell>();
		var unit = GridUnit;

		// グリッドの開始位置を単位に合わせて調整
		var startLat = Math.Floor(minLat / unit) * unit;
		var startLng = Math.Floor(minLng / unit) * unit;

		for (var lat = startLat; lat < maxLat; lat += unit)
		{
			for (var lng = startLng; lng < maxLng; lng += unit)
			{
				var cellMinLat = lat;
				var cellMaxLat = lat + unit;
				var cellMinLng = lng;
				var cellMaxLng = lng + unit;

				// このセル内に存在する観測点をカウント
				var pointCount = validPoints.Count(p =>
					p.Location.Latitude >= cellMinLat &&
					p.Location.Latitude < cellMaxLat &&
					p.Location.Longitude >= cellMinLng &&
					p.Location.Longitude < cellMaxLng);

				// 観測点が存在するセルのみを追加
				if (pointCount > 0)
				{
					gridCells.Add(new GridCell(cellMinLat, cellMaxLat, cellMinLng, cellMaxLng, pointCount));
				}
			}
		}

		GridLayer.UpdateGridCells(gridCells);
		GridCount = gridCells.Count;
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
}
