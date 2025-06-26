using Avalonia;
using Avalonia.Controls;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using SkiaSharp;
using System;
using System.Net.Http;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Controls;

public partial class KyoshinImageMapControl : UserControl
{
	#region 依存関係プロパティ

	public static readonly DirectProperty<KyoshinImageMapControl, Point> CenterPointProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapControl, Point>(
			nameof(CenterPoint),
			o => o.CenterPoint,
			(o, v) => o.CenterPoint = v,
			new Point(176, 200));

	private Point _centerPoint = new(176, 200);
	public Point CenterPoint
	{
		get => _centerPoint;
		set => SetAndRaise(CenterPointProperty, ref _centerPoint, value);
	}

	public static readonly DirectProperty<KyoshinImageMapControl, double> ScaleProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapControl, double>(
			nameof(Scale),
			o => o.Scale,
			(o, v) => o.Scale = v,
			1.0);

	private double _scale = 1.0;
	public double Scale
	{
		get => _scale;
		set => SetAndRaise(ScaleProperty, ref _scale, value);
	}

	public static readonly DirectProperty<KyoshinImageMapControl, ObservationPoint[]?> ObservationPointsProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapControl, ObservationPoint[]?>(
			nameof(ObservationPoints),
			o => o.ObservationPoints,
			(o, v) => o.ObservationPoints = v);

	private ObservationPoint[]? _observationPoints;
	public ObservationPoint[]? ObservationPoints
	{
		get => _observationPoints;
		set => SetAndRaise(ObservationPointsProperty, ref _observationPoints, value);
	}

	public static readonly DirectProperty<KyoshinImageMapControl, ObservationPoint?> SelectedObservationPointProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapControl, ObservationPoint?>(
			nameof(SelectedObservationPoint),
			o => o.SelectedObservationPoint,
			(o, v) => o.SelectedObservationPoint = v);

	private ObservationPoint? _selectedObservationPoint;
	public ObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set => SetAndRaise(SelectedObservationPointProperty, ref _selectedObservationPoint, value);
	}

	public static readonly DirectProperty<KyoshinImageMapControl, bool> ShowDebugInfoProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapControl, bool>(
			nameof(ShowDebugInfo),
			o => o.ShowDebugInfo,
			(o, v) => o.ShowDebugInfo = v,
			false);

	private bool _showDebugInfo = false;
	public bool ShowDebugInfo
	{
		get => _showDebugInfo;
		set => SetAndRaise(ShowDebugInfoProperty, ref _showDebugInfo, value);
	}

	#endregion

	#region イベント

	public event EventHandler<ObservationPointClickedEventArgs>? ObservationPointClicked;
	public event EventHandler<ObservationPointMovedEventArgs>? ObservationPointMoved;

	#endregion

	#region プライベートフィールド

	private readonly HttpClient _httpClient = new();
	private SKBitmap? _backgroundImage;
	private SKBitmap? _kyoshinImage;
	private RealtimeDataType _currentImageType = RealtimeDataType.Shindo;

	private const double BaseImageWidth = 352;
	private const double BaseImageHeight = 400;

	#endregion

	public KyoshinImageMapControl()
	{
		InitializeComponent();
		InitializeControls();
		LoadBackgroundImage();
		UpdateKyoshinImage();
	}

	public bool ShowMonitorImage { get; set; } = true;
	public bool ShowObservationPoints { get; set; } = true;

	private void InitializeControls()
	{
		// 画像種類コンボボックスの初期化
		foreach (var dataType in Enum.GetValues<RealtimeDataType>())
		{
			ImageTypeComboBox.Items.Add(dataType);
		}
		ImageTypeComboBox.SelectedItem = RealtimeDataType.Shindo;

		// スケールスライダーの初期化
		ScaleSlider.Value = Scale;
		UpdateScaleText();

		// 情報パネルの初期化
		UpdateSelectedPointText();

		// プロパティ変更の監視
		PropertyChanged += OnPropertyChanged;

		// マウス位置監視の設定
		MapCanvas.PointerMoved += (_, e) =>
		{
			var position = e.GetPosition(MapCanvas);
			var imagePos = MapCanvas.GetMouseImagePosition(position);
			MousePositionText.Text = $"マウス位置: ({imagePos.X:F0}, {imagePos.Y:F0})";
		};

		ShowMonitorImageCheckBox.IsCheckedChanged += (_, _) =>
		{
			ShowMonitorImage = ShowMonitorImageCheckBox.IsChecked == true;
			MapCanvas.ShowMonitorImage = ShowMonitorImage;
		};
		ShowObservationPointCheckBox.IsCheckedChanged += (_, _) =>
		{
			ShowObservationPoints = ShowObservationPointCheckBox.IsChecked == true;
			MapCanvas.ShowObservationPoints = ShowObservationPoints;
		};
	}

	private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == ScaleProperty)
		{
			UpdateScaleText();
		}
		else if (e.Property == SelectedObservationPointProperty)
		{
			UpdateSelectedPointText();
		}
	}

	private void UpdateScaleText() => ScaleText.Text = $"x{Scale:F1}";

	private void UpdateSelectedPointText()
	{
		if (SelectedObservationPoint == null)
		{
			SelectedPointText.Text = "選択観測点: なし";
		}
		else
		{
			var point = SelectedObservationPoint;
			SelectedPointText.Text = $"選択観測点: {point.Code} - {point.Name} ({point.Type})";
		}
	}

	#region 画像読み込み

	private async void LoadBackgroundImage()
	{
		try
		{
			var backgroundUrl = "http://www.kmoni.bosai.go.jp/data/map_img/CommonImg/base_map_w.gif";
			var imageData = await _httpClient.GetByteArrayAsync(backgroundUrl);
			_backgroundImage = SKBitmap.Decode(imageData);
			
			// 画像サイズ情報の更新
			ImageSizeText.Text = $"画像サイズ: {_backgroundImage.Width}x{_backgroundImage.Height}";
			
			// Canvas に画像を設定
			MapCanvas.BackgroundImage = _backgroundImage;
		}
		catch (Exception ex)
		{
			// エラーハンドリング（ログ出力など）
			System.Diagnostics.Debug.WriteLine($"背景画像の読み込みに失敗: {ex.Message}");
			ImageSizeText.Text = "画像サイズ: 読み込み失敗";
		}
	}

	private async void UpdateKyoshinImage()
	{
		try
		{
			var imageUrl = WebApiUrlGenerator.Generate(
				WebApiUrlType.RealtimeImg, 
				DateTime.Now.AddMinutes(-1), 
				_currentImageType);
			
			var imageData = await _httpClient.GetByteArrayAsync(imageUrl);
			_kyoshinImage = SKBitmap.Decode(imageData);
			
			// Canvas に画像を設定
			MapCanvas.KyoshinImage = _kyoshinImage;
		}
		catch (Exception ex)
		{
			// エラーハンドリング（ログ出力など）
			System.Diagnostics.Debug.WriteLine($"強震画像の読み込みに失敗: {ex.Message}");
		}
	}

	#endregion




	#region イベントハンドラ

	private void ScaleSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => Scale = e.NewValue;

	private void RefreshImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => UpdateKyoshinImage();

	private void ImageTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (ImageTypeComboBox.SelectedItem is RealtimeDataType dataType)
		{
			_currentImageType = dataType;
			UpdateKyoshinImage();
		}
	}

	#region Canvas イベントハンドラ

	private void MapCanvas_ObservationPointClicked(object? sender, ObservationPointClickedEventArgs e) =>
		ObservationPointClicked?.Invoke(this, e);

	private void MapCanvas_ObservationPointMoved(object? sender, ObservationPointMovedEventArgs e) =>
		ObservationPointMoved?.Invoke(this, e);

	#endregion

	#endregion

	#region 保護されたイベント発火メソッド

	protected virtual void OnObservationPointClicked(ObservationPoint? point, PointerButton button, Point2? newPosition = null) =>
		ObservationPointClicked?.Invoke(this, new ObservationPointClickedEventArgs(point, button, newPosition));

	protected virtual void OnObservationPointMoved(ObservationPoint point, Point2 newPosition) =>
		ObservationPointMoved?.Invoke(this, new ObservationPointMovedEventArgs(point, newPosition));

	#endregion

	protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
	{
		base.OnUnloaded(e);
		_httpClient?.Dispose();
		_backgroundImage?.Dispose();
		_kyoshinImage?.Dispose();
	}
}

#region イベント引数クラス

public class ObservationPointClickedEventArgs(ObservationPoint? point, PointerButton button, Point2? newPosition = null) : EventArgs
{
	public ObservationPoint? ObservationPoint { get; } = point;
	public PointerButton Button { get; } = button;
	public Point2? NewPosition { get; } = newPosition;
}

public class ObservationPointMovedEventArgs(ObservationPoint point, Point2 newPosition) : EventArgs
{
	public ObservationPoint ObservationPoint { get; } = point;
	public Point2 NewPosition { get; } = newPosition;
}

#endregion

public enum PointerButton
{
	Left,
	Right,
	Middle
}
