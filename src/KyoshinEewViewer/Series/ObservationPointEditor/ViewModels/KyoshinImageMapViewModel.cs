using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinMonitorLib.UrlGenerator;
using R3;
using SkiaSharp;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.ObservationPointEditor.ViewModels;

public partial class KyoshinImageMapViewModel : ObservableObject
{
	#region 観測点関連プロパティ

	[ObservableProperty]
	public partial CommonObservationPoint[] ObservationPoints { get; set; } = [];

	[ObservableProperty]
	public partial CommonObservationPoint? SelectedObservationPoint { get; set; }

	#endregion

	#region 表示設定プロパティ

	[ObservableProperty]
	public partial bool ShowMonitorImage { get; set; } = true;

	[ObservableProperty]
	public partial bool ShowObservationPoints { get; set; } = true;

	[ObservableProperty]
	public partial bool ShowDebugInfo { get; set; } = false;

	#endregion

	#region ビューポートプロパティ

	[ObservableProperty]
	public partial double Scale { get; set; } = 1.0;

	[ObservableProperty]
	public partial Point CenterPoint { get; set; } = new(176, 200);

	#endregion

	#region 画像プロパティ

	[ObservableProperty]
	public partial SKBitmap? BackgroundImage { get; private set; }

	[ObservableProperty]
	public partial SKBitmap? KyoshinImage { get; private set; }

	[ObservableProperty]
	public partial RealtimeDataType CurrentImageType { get; set; } = RealtimeDataType.Shindo;

	#endregion

	#region デバッグ情報プロパティ

	[ObservableProperty]
	public partial string MousePositionText { get; set; } = "マウス位置: N/A";

	[ObservableProperty]
	public partial string ImageSizeText { get; set; } = "画像サイズ: N/A";

	[ObservableProperty]
	public partial string SelectedPointText { get; set; } = "選択観測点: なし";

	[ObservableProperty]
	public partial string ScaleText { get; set; } = "x1.0";

	#endregion

	#region レイアウト管理プロパティ

	[ObservableProperty]
	public partial Rect LeftBottomRect { get; set; }

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
		this.ObservePropertyChanged(x => x.Scale)
			.Subscribe(scale => ScaleText = $"x{scale:F1}");

		this.ObservePropertyChanged(x => x.SelectedObservationPoint)
			.Subscribe(UpdateSelectedPointText);

		this.ObservePropertyChanged(x => x.CurrentImageType)
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

