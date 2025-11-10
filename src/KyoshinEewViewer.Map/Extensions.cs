using Avalonia;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Map.Simplify;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Map;

public static class Extensions
{
	public static PointD ToPixel(this Location loc, double zoom, MapProjection? projection = null)
		=> (projection ?? MapProjection.Default).LatLngToPixel(loc, zoom);
	public static Location ToLocation(this PointD loc, double zoom, MapProjection? projection = null)
		=> (projection ?? MapProjection.Default).PixelToLatLng(loc, zoom);

	public static PointD CastPoint(this Location loc)
		=> new(loc.Latitude, loc.Longitude);
	public static Location CastLocation(this PointD loc)
		=> new((float)loc.X, (float)loc.Y);

	public static Location[] ToLocations(this IntVector[] points, TopologyMap map)
	{
		var result = new Location[points.Length];
		points.ToLocationsInto(map, result);
		return result;
	}

	public static int ToLocationsInto(this IntVector[] points, TopologyMap map, Location[] buffer)
	{
		if (buffer.Length < points.Length)
			throw new ArgumentException($"バッファサイズ不足: 必要={points.Length}, 実際={buffer.Length}", nameof(buffer));

		double x = 0;
		double y = 0;
		for (var i = 0; i < points.Length; i++)
			buffer[i] = new Location((float)((x += points[i].X) * map.Scale.X + map.Translate.X), (float)((y += points[i].Y) * map.Scale.Y + map.Translate.Y));
		return points.Length;
	}

	public static SKPoint[]? ToPixedAndReduction(this Location[] nodes, double zoom, bool closed)
	{
		var pixelPoints = ArrayPool<PointD>.Shared.Rent(nodes.Length);
		try
		{
			for (var i = 0; i < nodes.Length; i++)
				pixelPoints[i] = nodes[i].ToPixel(zoom);
			var points = DouglasPeucker.Reduction(pixelPoints.AsSpan(0, nodes.Length), 1, closed);
			if (points.Length <= 1 ||
				(closed && points.Length <= 4)
			) // 小さなポリゴンは描画しない
				return null!;
			return points;
		}
		finally
		{
			ArrayPool<PointD>.Shared.Return(pixelPoints);
		}
	}

	public static Rect CalcRect(this IEnumerable<Location> locations)
	{
		if (!locations.Any())
			return new();

		var minX = double.MaxValue;
		var minY = double.MaxValue;
		var maxX = double.MinValue;
		var maxY = double.MinValue;
		foreach (var loc in locations)
		{
			minX = Math.Min(minX, loc.Latitude);
			minY = Math.Min(minY, loc.Longitude);
			maxX = Math.Max(maxX, loc.Latitude);
			maxY = Math.Max(maxY, loc.Longitude);
		}
		return new Rect(minX, minY, maxX - minX, maxY - minY);
	}
}
