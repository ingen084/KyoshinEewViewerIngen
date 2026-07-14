using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Layers;

public class ObservationPointEditorLayer : MapLayer
{
	private ObservationPointEditorModel? _model;

	// 描画用リソース
	private SKPaint? _kikNetPaint;
	private SKPaint? _kNetPaint;
	private SKPaint? _suspendedPaint;
	// 強震モニタ座標未設定の観測点用の点線ペイント。描画のたびに生成するとネイティブリソースの解放が
	// ファイナライザ任せになりパン中にメモリ使用量が増え続けるため、一度だけ生成して使い回す
	private SKPaint? _kikNetDashedPaint;
	private SKPaint? _kNetDashedPaint;
	private SKPaint? _suspendedDashedPaint;
	private SKPaint? _selectedPaint;
	private SKPaint? _selectedBorderPaint;
	private SKPaint? _textPaint;
	private SKPaint? _selectedTextPaint;
	private SKPaint? _textBackgroundPaint;
	// 観測点ラベル用フォント。テーマに依存しないため一度だけ生成する
	private static readonly SKFont LabelFont = new(KyoshinEewViewerFonts.MainRegular, 12);

	// ラベル文字列の整形(シェイピング)結果のキャッシュ。毎フレームMeasureText/DrawTextで文字列を整形し直すと
	// パン中のCPU負荷と一時割り当てが大きいため、観測点名単位で使い回す(Renderを実行するスレッドからのみ触る)
	// 観測点名の種類数以上には増えないため、明示的な破棄は行わずレイヤーと同じ寿命とする
	private readonly Dictionary<string, (SKTextBlob Blob, SKRect Bounds)> _labelBlobCache = [];

	// 設定
	private const float MinZoomForAllLabels = 8.0f; // 全観測点のラベル表示最小ズーム
	private const double ClickThresholdPixel = 5; // クリック判定の閾値(ピクセル)

	// クリック判定・描画用の座標キャッシュ。FilteredObservationPoints は参照固定で中身が変異するため、
	// 変化のたびにスナップショット配列を取り直してキャッシュのキーとする
	private CommonObservationPoint[]? _pointsSnapshot;
	private readonly PointLayoutCache<CommonObservationPoint> _pointLayoutCache = new(p => p.Location);

	private CommonObservationPoint[] PointsSnapshot => _pointsSnapshot ??= [.. _model!.FilteredObservationPoints];

	public override bool NeedPersistentUpdate => false;

	public ObservationPointEditorLayer(ObservationPointEditorModel model)
	{
		_model = model ?? throw new ArgumentNullException(nameof(model));
		_model.PropertyChanged += (_, _) => RefreshRequest();
		_model.FilteredObservationPoints.CollectionChanged += OnFilteredObservationPointsChanged;
	}

	private void OnFilteredObservationPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// ApplyFilterはClear+Addで1件ごとにイベントが発火するため、スナップショット破棄済みの間は再描画要求を重ねない
		if (_pointsSnapshot == null)
			return;
		_pointsSnapshot = null;
		RefreshRequest();
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

		// 点線の破線パターンは3種で共有できる
		var dashEffect = SKPathEffect.CreateDash([4, 4], 0);
		_kikNetDashedPaint = CreateDashedPaint(SKColors.Red, dashEffect);
		_kNetDashedPaint = CreateDashedPaint(SKColors.Orange, dashEffect);
		_suspendedDashedPaint = CreateDashedPaint(SKColors.Gray, dashEffect);

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
			IsAntialias = true,
		};

		// 選択中のテキスト表示用（目立つ色）
		_selectedTextPaint = new SKPaint
		{
			Color = SKColors.Yellow,
			IsAntialias = true,
		};

		_textBackgroundPaint = new SKPaint
		{
			Color = theme.IsDark ? new SKColor(0, 0, 0, 200) : new SKColor(255, 255, 255, 200),
			Style = SKPaintStyle.Fill,
			IsAntialias = true
		};
	}

	private static SKPaint CreateDashedPaint(SKColor color, SKPathEffect dashEffect) => new()
	{
		Color = color,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 2,
		IsAntialias = true,
		PathEffect = dashEffect,
	};

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
			var snapshot = PointsSnapshot;
			var pixels = _pointLayoutCache.Get(snapshot, zoom).Pixels;
			for (var i = 0; i < snapshot.Length; i++)
			{
				var point = snapshot[i];
				var pixelPoint = pixels[i];

				if (double.IsNaN(pixelPoint.X))
					continue;

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

	private void DrawObservationPoint(SKCanvas canvas, CommonObservationPoint point, PointD pixelPoint, double zoom, bool isSelected)
	{
		// サイズをズームレベルに応じて調整
		var radius = (float)Math.Max(3, Math.Min(8, zoom * 0.6));

		// 観測点種別による塗りつぶし色の選択
		var fillPaint = point.IsSuspended ? _suspendedPaint :
						point.Type == ObservationPointType.KiK_net ? _kikNetPaint :
						_kNetPaint;

		// 強震モニタ座標の設定状態によって表示を変える
		if (point.Point != null)
		{
			// 強震モニタ座標が設定されている場合：通常の円
			canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius, fillPaint!);
		}
		else
		{
			// 強震モニタ座標が未設定の場合：点線の円
			var dashedPaint = point.IsSuspended ? _suspendedDashedPaint :
							point.Type == ObservationPointType.KiK_net ? _kikNetDashedPaint :
							_kNetDashedPaint;
			canvas.DrawCircle((float)pixelPoint.X, (float)pixelPoint.Y, radius, dashedPaint!);
		}

		// 選択中の場合
		if (isSelected)
		{
			if (point.Point != null)
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

	/// <summary>
	/// ラベル文字列の整形済みSKTextBlobと計測結果を取得する。空文字列や整形不能な場合はnullを返す
	/// </summary>
	private (SKTextBlob Blob, SKRect Bounds)? GetLabelBlob(string text)
	{
		if (string.IsNullOrEmpty(text))
			return null;
		if (_labelBlobCache.TryGetValue(text, out var cached))
			return cached;

		if (SKTextBlob.Create(text, LabelFont) is not { } blob)
			return null;
		LabelFont.MeasureText(text, out var bounds);
		var entry = (blob, bounds);
		_labelBlobCache[text] = entry;
		return entry;
	}

	private void DrawLabel(SKCanvas canvas, CommonObservationPoint point, PointD pixelPoint, bool isSelected)
	{
		if (_textPaint == null || _textBackgroundPaint == null)
			return;
		if (GetLabelBlob(point.Name) is not { } label)
			return;

		var textPaint = isSelected ? _selectedTextPaint : _textPaint;
		var textBounds = label.Bounds;

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
		canvas.DrawText(label.Blob, textX, textY, textPaint!);
	}

	public override bool OnMouseClick(KyoshinMonitorLib.Location location, PointD screenPosition, MouseButton button, LayerRenderParameter param)
	{
		if (_model == null || button != MouseButton.Left)
			return false;

		// クリックした位置の近くにある観測点を探す（複数の候補）
		var snapshot = PointsSnapshot;
		var layout = _pointLayoutCache.Get(snapshot, param.Zoom);

		var candidates = new List<(int Index, double Distance)>();
		layout.CollectWithin(screenPosition + param.LeftTopPixel, ClickThresholdPixel, candidates);

		if (candidates.Count == 0)
			return false;

		if (candidates.Count == 1)
		{
			// 1つだけの場合は直接選択
			_model.SelectedObservationPoint = snapshot[candidates[0].Index];
			return true;
		}

		// 複数の候補がある場合はメニューを表示（結果順序は不定なので距離昇順に並べ替える）
		ShowCandidateMenu(candidates.OrderBy(c => c.Distance).Select(c => snapshot[c.Index]).ToList(), screenPosition);
		return true;
	}

	private async void ShowCandidateMenu(List<CommonObservationPoint> candidates, PointD screenPosition)
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window window)
			return;

		var dialog = new FAContentDialog
		{
			Title = "観測点の選択",
			Content = CreateCandidateList(candidates),
			PrimaryButtonText = "選択",
			SecondaryButtonText = "キャンセル",
			DefaultButton = FAContentDialogButton.Primary
		};

		var result = await dialog.ShowAsync(window);

		if (result == FAContentDialogResult.Primary && dialog.Content is ListBox listBox && listBox.SelectedItem is CommonObservationPoint selectedPoint)
		{
			if (_model != null)
				_model.SelectedObservationPoint = selectedPoint;
		}
	}

	private static ListBox CreateCandidateList(List<CommonObservationPoint> candidates)
	{
		var listBox = new ListBox
		{
			ItemsSource = candidates,
			SelectedIndex = 0,
			Height = Math.Min(300, candidates.Count * 40 + 20)
		};

		var template = new FuncDataTemplate<CommonObservationPoint>((point, _) =>
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
