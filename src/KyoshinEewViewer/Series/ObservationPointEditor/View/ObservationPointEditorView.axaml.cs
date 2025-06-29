using Avalonia.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using System;

namespace KyoshinEewViewer.Series.ObservationPointEditor.View;

public partial class ObservationPointEditorView : UserControl
{
	public KyoshinImageMapCanvas? ImageMapCanvas => this.FindControl<KyoshinImageMapCanvas>("EditorImageMapCanvas");

	public ObservationPointEditorView()
	{
		InitializeComponent();
		InitializeControls();
	}

	private void InitializeControls()
	{
		// 画像種類コンボボックスの初期化
		foreach (var dataType in Enum.GetValues<RealtimeDataType>())
		{
			ImageTypeComboBox.Items.Add(dataType);
		}
		ImageTypeComboBox.SelectedItem = RealtimeDataType.Shindo;

		// マウス位置監視の設定
		if (ImageMapCanvas != null)
		{
			ImageMapCanvas.PointerMoved += (_, e) =>
			{
				if (DataContext is ObservationPointEditorSeries series)
				{
					var position = e.GetPosition(ImageMapCanvas);
					var imagePos = ImageMapCanvas.GetMouseImagePosition(position);
					series.MapViewModel.UpdateMousePosition(imagePos);
				}
			};
		}
	}

	private void ImageMapCanvas_ObservationPointMoved(object? sender, ObservationPointMovedEventArgs e)
	{
		if (DataContext is not ObservationPointEditorSeries series) return;

		// 観測点の座標を更新
		e.ObservationPoint.Point = e.NewPosition;
		series.Model.UpdateObservationPoint();
	}


	private void RefreshImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (DataContext is ObservationPointEditorSeries series)
			_ = series.MapViewModel.RefreshImage();
	}

	private void ImageTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is ObservationPointEditorSeries series && ImageTypeComboBox.SelectedItem is RealtimeDataType dataType)
		{
			series.MapViewModel.UpdateImageType(dataType);
		}
	}
}