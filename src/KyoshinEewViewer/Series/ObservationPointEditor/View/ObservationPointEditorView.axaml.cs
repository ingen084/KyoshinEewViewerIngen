using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinMonitorLib;
using System.Threading.Tasks;
using KyoshinEewViewer;

namespace KyoshinEewViewer.Series.ObservationPointEditor.View;

public partial class ObservationPointEditorView : UserControl
{
	public ObservationPointEditorView()
	{
		InitializeComponent();
	}

	private async void ImageMapControl_ObservationPointClicked(object? sender, ObservationPointClickedEventArgs e)
	{
		if (DataContext is not ObservationPointEditorSeries series) return;

		switch (e.Button)
		{
			case PointerButton.Left:
				// 左クリック：選択
				series.Model.SelectedObservationPoint = e.ObservationPoint;
				break;

			case PointerButton.Right:
				// 右クリック：削除（確認後）
				if (e.ObservationPoint != null)
				{
					await ShowDeleteConfirmationDialog(series, e.ObservationPoint);
				}
				break;

			case PointerButton.Middle:
				// 中クリック：新規追加
				if (e.NewPosition.HasValue)
				{
					var newPoint = series.Model.CreateNewObservationPoint();
					newPoint.Point = e.NewPosition;
					series.Model.AddObservationPoint(newPoint);
					series.Model.SelectedObservationPoint = newPoint;
				}
				break;
		}
	}

	private void ImageMapControl_ObservationPointMoved(object? sender, ObservationPointMovedEventArgs e)
	{
		if (DataContext is not ObservationPointEditorSeries series) return;

		// 観測点の座標を更新
		e.ObservationPoint.Point = e.NewPosition;
		series.Model.UpdateObservationPoint();
	}

	private async Task ShowDeleteConfirmationDialog(ObservationPointEditorSeries series, ObservationPoint point)
	{
		var result = await new ContentDialog
		{
			Title = "観測点の削除",
			Content = $"観測点「{point.Name} ({point.Code})」を削除しますか？",
			PrimaryButtonText = "削除",
			SecondaryButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Secondary
		}.ShowAsync(KyoshinEewViewerApp.TopLevelControl as Window);

		if (result == ContentDialogResult.Primary)
		{
			series.Model.RemoveObservationPoint(point);
		}
	}
}