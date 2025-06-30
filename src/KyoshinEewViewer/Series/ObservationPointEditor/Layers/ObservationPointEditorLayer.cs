using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Layers;

public class ObservationPointEditorLayer : MapLayer
{
	private ObservationPointEditorModel? _model;

	// 描画用リソース
	private SKPaint? _kikNetPaint;
	private SKPaint? _kNetPaint;
	private SKPaint? _suspendedPaint;
	private SKPaint? _selectedPaint;
	private SKPaint? _selectedBorderPaint;
	private SKPaint? _textPaint;
	private SKPaint? _selectedTextPaint;
	private SKPaint? _textBackgroundPaint;

	// 設定
	private const float MinZoomForAllLabels = 8.0f; // 全観測点のラベル表示最小ズーム

	public override bool NeedPersistentUpdate => false;

	public ObservationPointEditorLayer(ObservationPointEditorModel model)
	{
		_model = model ?? throw new ArgumentNullException(nameof(model));
		_model.PropertyChanged += (_, _) => RefreshRequest();
	}

	public override void RefreshResourceCache(WindowTheme theme)
	{
		// 観測点種別ごとの色設定（円用）
		_kikNetPaint = new SKPaint
		{
			Color = SKColors.Red,
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};

		_kNetPaint = new SKPaint
		{
			Color = SKColors.Orange,
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};

		_suspendedPaint = new SKPaint
		{
			Color = SKColors.Gray,
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};

		// 選択中の観測点用（塗りつぶし）
		_selectedPaint = new SKPaint
		{
			Color = SKColors.Cyan,
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};

		// 選択中の観測点の枠線（四角）
		_selectedBorderPaint = new SKPaint
		{
			Color = SKColors.Magenta,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2,
			IsAntialias = true
		};

		// 通常のテキスト表示用
		_textPaint = new SKPaint
		{
			Color = theme.IsDark ? SKColors.White : SKColors.Black,
			TextSize = 12,
			IsAntialias = true,
			Typeface = KyoshinEewViewerFonts.MainRegular,
		};

		// 選択中のテキスト表示用（目立つ色）
		_selectedTextPaint = new SKPaint
		{
			Color = SKColors.Yellow,
			TextSize = 12,
			IsAntialias = true,
			Typeface = KyoshinEewViewerFonts.MainRegular,
		};

		_textBackgroundPaint = new SKPaint
		{
			Color = theme.IsDark ? new SKColor(0, 0, 0, 200) : new SKColor(255, 255, 255, 200),
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (_model == null)
			return;

		var zoom = param.Zoom;
		var showAllLabels = zoom >= MinZoomForAllLabels;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			// フィルター済みの観測点を描画
			foreach (var point in _model.FilteredObservationPoints)
			{
				if (point.Location == null)
					continue;

				// 画面座標に変換
				var pixelPoint = point.Location.ToPixel(zoom);

				// 画面外の場合はスキップ
				if (!param.PixelBound.Contains(pixelPoint))
					continue;

				var isSelected = _model.SelectedObservationPoint == point;

				// 観測点の描画
				DrawObservationPoint(canvas, point, pixelPoint, zoom, isSelected);

				// ラベルの描画（選択中の観測点は常に表示）
				if (showAllLabels || isSelected)
				{
					DrawLabel(canvas, point, pixelPoint, isSelected);
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	private void DrawObservationPoint(SKCanvas canvas, ObservationPoint point, PointD pixelPoint, double zoom, bool isSelected)
	{
		// サイズをズームレベルに応じて調整
		var radius = (float)Math.Max(3, Math.Min(8, zoom * 0.6));

		// 観測点種別による塗りつぶし色の選択
		var fillPaint = point.IsSuspended ? _suspendedPaint :
						point.Type == ObservationPointType.KiK_net ? _kikNetPaint :
						_kNetPaint;

		// 強震モニタ座標の設定状態によって表示を変える
		var hasKyoshinCoordinate = point.Point.HasValue;

		if (hasKyoshinCoordinate)
		{
			// 強震モニタ座標が設定されている場合：通常の円
			canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius, fillPaint!);
		}
		else
		{
			// 強震モニタ座標が未設定の場合：点線の円
			using var dashPaint = new SKPaint
			{
				Color = fillPaint!.Color,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = 2,
				IsAntialias = true,
				PathEffect = SKPathEffect.CreateDash([4, 4], 0)
			};
			canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius, dashPaint);
		}

		// 選択中の場合
		if (isSelected)
		{
			if (hasKyoshinCoordinate)
			{
				// 内側を選択色で塗りつぶし
				canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius - 1, _selectedPaint!);
			}

			// 外側に四角の枠線を描画
			var borderSize = radius * 2 + 6;
			var borderRect = SKRect.Create(
				(float)(pixelPoint.X - borderSize / 2),
				(float)(pixelPoint.Y - borderSize / 2),
				borderSize,
				borderSize
			);
			canvas.DrawRect(borderRect, _selectedBorderPaint!);
		}
	}

	private void DrawLabel(SKCanvas canvas, ObservationPoint point, PointD pixelPoint, bool isSelected)
	{
		if (_textPaint == null || _textBackgroundPaint == null)
			return;

		var text = point.Name;
		var textPaint = isSelected ? _selectedTextPaint : _textPaint;
		var textBounds = new SKRect();
		textPaint!.MeasureText(text, ref textBounds);

		// テキストの位置（観測点の下に表示）
		var textX = (float)(pixelPoint.X - textBounds.Width / 2);
		var textY = (float)(pixelPoint.Y + 12 + textBounds.Height);

		// 背景の矩形
		var backgroundRect = SKRect.Create(
			textX - 2,
			textY - textBounds.Height - 2,
			textBounds.Width + 4,
			textBounds.Height + 4
		);

		// 背景を描画
		canvas.DrawRoundRect(backgroundRect, 2, 2, _textBackgroundPaint);

		// テキストを描画
		canvas.DrawText(text, textX, textY, textPaint);
	}

	public override bool OnMouseClick(KyoshinMonitorLib.Location location, PointD screenPosition, MouseButton button, LayerRenderParameter param)
	{
		if (_model == null || button != MouseButton.Left)
			return false;

		// クリックした位置の近くにある観測点を探す（複数の候補）
		var candidates = new List<(ObservationPoint Point, double Distance)>();

		foreach (var point in _model.FilteredObservationPoints)
		{
			if (point.Location == null)
				continue;

			// 画面座標での距離を計算
			var pixelPoint = point.Location.ToPixel(param.Zoom) - param.LeftTopPixel;
			var distance = Math.Sqrt(Math.Pow(pixelPoint.X - screenPosition.X, 2) + Math.Pow(pixelPoint.Y - screenPosition.Y, 2));

			// クリック判定（5ピクセル以内）
			if (distance < 5)
				candidates.Add((point, distance));
		}

		if (candidates.Count == 0)
			return false;

		if (candidates.Count == 1)
		{
			// 1つだけの場合は直接選択
			_model.SelectedObservationPoint = candidates[0].Point;
			return true;
		}

		// 複数の候補がある場合はメニューを表示
		ShowCandidateMenu(candidates.OrderBy(c => c.Distance).Select(c => c.Point).ToList(), screenPosition);
		return true;
	}

	private async void ShowCandidateMenu(List<ObservationPoint> candidates, PointD screenPosition)
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window window)
			return;

		var dialog = new ContentDialog
		{
			Title = "観測点の選択",
			Content = CreateCandidateList(candidates),
			PrimaryButtonText = "選択",
			SecondaryButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary
		};

		var result = await dialog.ShowAsync(window);

		if (result == ContentDialogResult.Primary && dialog.Content is ListBox listBox && listBox.SelectedItem is ObservationPoint selectedPoint)
		{
			if (_model != null)
				_model.SelectedObservationPoint = selectedPoint;
		}
	}

	private static ListBox CreateCandidateList(List<ObservationPoint> candidates)
	{
		var listBox = new ListBox
		{
			ItemsSource = candidates,
			SelectedIndex = 0,
			Height = Math.Min(300, candidates.Count * 40 + 20)
		};

		var template = new FuncDataTemplate<ObservationPoint>((point, _) =>
		{
			if (point == null)
				return null;

			var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };

			// 観測点種別の色付きインジケーター
			var indicator = new Border
			{
				Width = 12,
				Height = 12,
				CornerRadius = new CornerRadius(6),
				Background = point.IsSuspended ? new SolidColorBrush(Colors.Gray) :
							point.Type == ObservationPointType.KiK_net ? new SolidColorBrush(Colors.Red) :
							new SolidColorBrush(Colors.Orange),
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			var textBlock = new TextBlock
			{
				Text = $"{point.Code} - {point.Name} ({point.Region})",
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			panel.Children.Add(indicator);
			panel.Children.Add(textBlock);

			return panel;
		});

		listBox.ItemTemplate = template;

		return listBox;
	}
}
