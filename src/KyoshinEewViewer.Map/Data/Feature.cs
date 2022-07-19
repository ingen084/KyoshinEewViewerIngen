using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Data;
public abstract class Feature
{
	public RectD BB { get; protected set; }
	public bool IsClosed { get; protected set; }

	public int? Code { get; protected set; }

	protected Dictionary<int, SKPath> PathCache { get; } = new();

	public abstract SKPath? GetOrCreatePath(int zoom);

	public abstract SKPoint[][]? GetOrCreatePointsCache(int zoom);

	public void ClearCache()
	{
		foreach (var p in PathCache.Values)
			p.Dispose();
		PathCache.Clear();
	}

	public void Draw(SKCanvas canvas, int zoom, SKPaint paint)
	{
		if (GetOrCreatePath(zoom) is not SKPath path)
			return;
		canvas.DrawPath(path, paint);
	}

	~Feature()
	{
		ClearCache();
	}
}
