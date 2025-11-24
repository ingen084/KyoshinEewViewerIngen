using Avalonia.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinMonitorLib.UrlGenerator;
using System;

namespace KyoshinEewViewer.Series.ObservationPointEditor.View;

public partial class ObservationPointEditorView : UserControl
{
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
		if (EditorImageMapCanvas != null)
		{
			EditorImageMapCanvas.PointerMoved += (_, e) =>
			{
				if (DataContext is ObservationPointEditorSeries series)
				{
					var position = e.GetPosition(EditorImageMapCanvas);
					var imagePos = EditorImageMapCanvas.GetMouseImagePosition(position);
					series.MapViewModel.UpdateMousePosition(imagePos);
				}
			};
		}
	}

	private void ImageMapCanvas_ObservationPointMoved(object? sender, ObservationPointMovedEventArgs e)
	{
		if (DataContext is not ObservationPointEditorSeries series) return;

		// 変更前の座標を記録（Undo/Redo用）
		var oldPoint = e.CommonObservationPoint.Point;
		
		// 観測点の座標を更新
		e.CommonObservationPoint.Point = e.NewPosition;
		
		// 変更履歴を記録
		series.Model.RecordChange(e.CommonObservationPoint, oldPoint, e.NewPosition);
		
		// データ更新（ApplyFilterは呼ばない）
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

	private void ObservationPointDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is not ObservationPointEditorSeries series) return;
		if (series.Model.SelectedObservationPoint == null) return;

		// DataGridの選択項目を表示領域にスクロール
		ObservationPointDataGrid.ScrollIntoView(series.Model.SelectedObservationPoint, null);
	}
}