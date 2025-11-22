using Avalonia;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinMonitorLib.UrlGenerator;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.ObservationPointEditor.ViewModels;

public class KyoshinImageMapViewModel : ReactiveObject
{
	#region 観測点関連プロパティ

	private CommonObservationPoint[] _observationPoints = [];
	public CommonObservationPoint[] ObservationPoints
	{
		get => _observationPoints;
		set => this.RaiseAndSetIfChanged(ref _observationPoints, value);
	}

	private CommonObservationPoint? _selectedObservationPoint;
	public CommonObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set => this.RaiseAndSetIfChanged(ref _selectedObservationPoint, value);
	}

	#endregion

	#region 表示設定プロパティ

	private bool _showMonitorImage = true;
	public bool ShowMonitorImage
	{
		get => _showMonitorImage;
		set => this.RaiseAndSetIfChanged(ref _showMonitorImage, value);
	}

	private bool _showObservationPoints = true;
	public bool ShowObservationPoints
	{
		get => _showObservationPoints;
		set => this.RaiseAndSetIfChanged(ref _showObservationPoints, value);
	}

	private bool _showDebugInfo = false;
	public bool ShowDebugInfo
	{
		get => _showDebugInfo;
		set => this.RaiseAndSetIfChanged(ref _showDebugInfo, value);
	}

	#endregion

	#region ビューポートプロパティ

	private double _scale = 1.0;
	public double Scale
	{
		get => _scale;
		set => this.RaiseAndSetIfChanged(ref _scale, value);
	}

	private Point _centerPoint = new(176, 200);
	public Point CenterPoint
	{
		get => _centerPoint;
		set => this.RaiseAndSetIfChanged(ref _centerPoint, value);
	}

	#endregion

	#region 画像プロパティ

	private SKBitmap? _backgroundImage;
	public SKBitmap? BackgroundImage
	{
		get => _backgroundImage;
		private set => this.RaiseAndSetIfChanged(ref _backgroundImage, value);
	}

	private SKBitmap? _kyoshinImage;
	public SKBitmap? KyoshinImage
	{
		get => _kyoshinImage;
		private set => this.RaiseAndSetIfChanged(ref _kyoshinImage, value);
	}

	private RealtimeDataType _currentImageType = RealtimeDataType.Shindo;
	public RealtimeDataType CurrentImageType
	{
		get => _currentImageType;
		set => this.RaiseAndSetIfChanged(ref _currentImageType, value);
	}

	#endregion

	#region デバッグ情報プロパティ

	private string _mousePositionText = "マウス位置: N/A";
	public string MousePositionText
	{
		get => _mousePositionText;
		set => this.RaiseAndSetIfChanged(ref _mousePositionText, value);
	}

	private string _imageSizeText = "画像サイズ: N/A";
	public string ImageSizeText
	{
		get => _imageSizeText;
		set => this.RaiseAndSetIfChanged(ref _imageSizeText, value);
	}

	private string _selectedPointText = "選択観測点: なし";
	public string SelectedPointText
	{
		get => _selectedPointText;
		set => this.RaiseAndSetIfChanged(ref _selectedPointText, value);
	}

	private string _scaleText = "x1.0";
	public string ScaleText
	{
		get => _scaleText;
		set => this.RaiseAndSetIfChanged(ref _scaleText, value);
	}

	#endregion

	#region レイアウト管理プロパティ

	private Rect _leftBottomRect;
	public Rect LeftBottomRect
	{
		get => _leftBottomRect;
		set => this.RaiseAndSetIfChanged(ref _leftBottomRect, value);
	}

	#endregion

	#region イベント

	public event EventHandler<ObservationPointClickedEventArgs>? ObservationPointClicked;
	public event EventHandler<ObservationPointMovedEventArgs>? ObservationPointMoved;

	#endregion

	#region プライベートフィールド

	private readonly HttpClient _httpClient = new();

	#endregion

	public KyoshinImageMapViewModel()
	{
		// プロパティ変更の監視設定
		this.WhenAnyValue(x => x.Scale)
			.Subscribe(scale => ScaleText = $"x{scale:F1}");

		this.WhenAnyValue(x => x.SelectedObservationPoint)
			.Subscribe(UpdateSelectedPointText);

		this.WhenAnyValue(x => x.CurrentImageType)
			.Subscribe(async _ => await RefreshImage());

		// 初期化
		_ = LoadBackgroundImage();
		_ = RefreshImage();
	}

	#region コマンドメソッド (Avaloniaが自動でCommandとして認識)

	/// <summary>
	/// 強震画像を更新する
	/// </summary>
	public async Task RefreshImage()
	{
		try
		{
			var imageUrl = WebApiUrlGenerator.Generate(
				WebApiUrlType.RealtimeImg,
				DateTime.Now.AddMinutes(-1),
				CurrentImageType);

			var imageData = await _httpClient.GetByteArrayAsync(imageUrl);
			KyoshinImage = SKBitmap.Decode(imageData);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"強震画像の読み込みに失敗: {ex.Message}");
		}
	}

	/// <summary>
	/// 背景画像を読み込む
	/// </summary>
	public async Task LoadBackgroundImage()
	{
		try
		{
			var backgroundUrl = "http://www.kmoni.bosai.go.jp/data/map_img/CommonImg/base_map_w.gif";
			var imageData = await _httpClient.GetByteArrayAsync(backgroundUrl);
			BackgroundImage = SKBitmap.Decode(imageData);

			// 画像サイズ情報の更新
			if (BackgroundImage != null)
			{
				ImageSizeText = $"画像サイズ: {BackgroundImage.Width}x{BackgroundImage.Height}";
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"背景画像の読み込みに失敗: {ex.Message}");
			ImageSizeText = "画像サイズ: 読み込み失敗";
		}
	}

	/// <summary>
	/// 画像種類を更新する
	/// </summary>
	/// <param name="imageType">新しい画像種類</param>
	public void UpdateImageType(RealtimeDataType imageType)
	{
		CurrentImageType = imageType;
	}

	/// <summary>
	/// マウス位置を更新する
	/// </summary>
	/// <param name="imagePosition">画像上の座標</param>
	public void UpdateMousePosition(Point imagePosition)
	{
		MousePositionText = $"マウス位置: ({imagePosition.X:F0}, {imagePosition.Y:F0})";
	}

	#endregion

	#region イベント発火メソッド

	/// <summary>
	/// 観測点クリックイベントを発火する
	/// </summary>
	public void OnObservationPointClicked(CommonObservationPoint? point, Controls.PointerButton button, KyoshinImagePoint? newPosition = null)
	{
		ObservationPointClicked?.Invoke(this, new ObservationPointClickedEventArgs(point, button, newPosition));
	}

	/// <summary>
	/// 観測点移動イベントを発火する
	/// </summary>
	public void OnObservationPointMoved(CommonObservationPoint point, KyoshinImagePoint newPosition)
	{
		ObservationPointMoved?.Invoke(this, new ObservationPointMovedEventArgs(point, newPosition));
	}

	#endregion

	#region プライベートメソッド

	private void UpdateSelectedPointText(CommonObservationPoint? point)
	{
		if (point == null)
		{
			SelectedPointText = "選択観測点: なし";
		}
		else
		{
			SelectedPointText = $"選択観測点: {point.Code} - {point.Name} ({point.Type})";
		}
	}

	#endregion

	#region IDisposable

	public void Dispose()
	{
		_httpClient?.Dispose();
		BackgroundImage?.Dispose();
		KyoshinImage?.Dispose();
		GC.SuppressFinalize(this);
	}

	#endregion
}

