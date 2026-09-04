using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Models;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Layers;

/// <summary>
/// 指定河川洪水予報の対象河川を表示するレイヤー
/// </summary>
public class FloodLayer : MapLayer
{
	private FloodRiver[] _rivers = [];
	/// <summary>
	/// 表示する河川。河川が重なった場合に深刻なほうが隠れないよう、軽いものから並べ替えて保持する
	/// </summary>
	public FloodRiver[] Rivers
	{
		get => _rivers;
		set
		{
			_rivers = [.. value.OrderBy(r => r.WarningType)];
			RefreshRequest();
		}
	}

	public override bool NeedPersistentUpdate => false;

	// SKPaintは全レイヤーインスタンスで共有する(コンポジタスレッドのRenderとRefreshResourceCacheの競合、
	// および電文グループごとのレイヤー生成によるリークを避けるため、Dispose・再生成はせず色プロパティの差し替えのみ行う)
	private static readonly SKPaint BorderPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeCap = SKStrokeCap.Round,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};
	private static readonly SKPaint CancelPaint = CreateLinePaint();
	private static readonly SKPaint AdvisoryPaint = CreateLinePaint();
	private static readonly SKPaint WarningPaint = CreateLinePaint();
	private static readonly SKPaint MajorWarningPaint = CreateLinePaint();

	private static SKPaint CreateLinePaint() => new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeCap = SKStrokeCap.Round,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	// 色分けは一覧表示の FloodWarningColor に合わせる
	// (警報解除のみ、地図では地形と輝度差が付かないため前景色側を使う)
	private static SKPaint GetPaint(byte warningType)
		=> warningType switch
		{
			2 => AdvisoryPaint,
			3 => WarningPaint,
			4 => MajorWarningPaint,
			_ => CancelPaint,
		};

	/// <summary>
	/// ズームに応じた線の太さ
	/// </summary>
	private static float GetLineWidth(double zoom)
		=> (float)Math.Max(2, 3 + (zoom - 5) * .8);

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		BorderPaint.Color = windowTheme.IsDark ? SKColors.Black : SKColors.White;
		// 一覧では警報解除に DockTitleBackgroundColor を使っているが、パネルの背景色のため
		// 地図に塗ると地形とほとんど区別が付かない。他のレベルは色で判別できるのに対し
		// 警報解除は無彩色なので、前景色側を使う
		CancelPaint.Color = SKColor.Parse(windowTheme.SubForegroundColor);
		AdvisoryPaint.Color = SKColor.Parse(windowTheme.TsunamiAdvisoryColor);
		WarningPaint.Color = SKColor.Parse(windowTheme.TsunamiWarningColor);
		MajorWarningPaint.Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (Rivers is not { Length: > 0 } rivers)
			return;

		// 線の太さはズームだけから決まるため、同時に描画される他のレイヤーとも同じ値になる
		var width = GetLineWidth(param.Zoom);
		BorderPaint.StrokeWidth = width + 3;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			foreach (var river in rivers)
			{
				using var path = BuildPath(river, param.Zoom);
				if (path.IsEmpty)
					continue;

				var paint = GetPaint(river.WarningType);
				paint.StrokeWidth = width;
				canvas.DrawPath(path, BorderPaint);
				canvas.DrawPath(path, paint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}

	private static SKPath BuildPath(FloodRiver river, double zoom)
	{
		var path = new SKPath();
		foreach (var part in river.Parts)
		{
			if (part.Length < 2)
				continue;
			for (var i = 0; i < part.Length; i++)
			{
				var pixel = part[i].ToPixel(zoom);
				if (i == 0)
					path.MoveTo((float)pixel.X, (float)pixel.Y);
				else
					path.LineTo((float)pixel.X, (float)pixel.Y);
			}
		}
		return path;
	}
}
