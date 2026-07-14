using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Map.Layers;

/// <summary>
/// 点集合のズーム非依存な正規化座標(投影結果)と、近傍探索用の均等グリッド索引。
/// 投影計算(三角関数)は構築時の1回のみで、ズームが変わっても再利用できる。
/// 座標が取得できない点は NaN として保持し、探索対象から自然に除外される
/// </summary>
internal sealed class NormalizedPointSet<TItem>
{
	// グリッド索引を構築する点数の下限。これ未満は線形探索の方が速く、索引の構築コストが無駄になるため作らない
	private const int GridPointCountThreshold = 200;

	public TItem[] Source { get; }
	/// <summary>正規化座標(ズーム0のピクセル座標)。座標が取得できない点は NaN</summary>
	public PointD[] Points { get; }

	// 近傍探索用の均等グリッド索引(CSR形式)。オブジェクトグラフを避けてフラットな配列2本で保持する
	// _cellStarts[cell] .. _cellStarts[cell + 1] が _cellPointIndices 上のそのセルに属する点の範囲。点数が少ない場合は null で線形探索を行う
	private readonly int[]? _cellStarts;
	private readonly int[]? _cellPointIndices;
	private readonly int _cellsX;
	private readonly int _cellsY;
	private readonly double _minX;
	private readonly double _minY;
	private readonly double _cellWidth;
	private readonly double _cellHeight;

	private NormalizedPointSet(TItem[] source, PointD[] points, int[]? cellStarts, int[]? cellPointIndices, int cellsX, int cellsY, double minX, double minY, double cellWidth, double cellHeight)
	{
		Source = source;
		Points = points;
		_cellStarts = cellStarts;
		_cellPointIndices = cellPointIndices;
		_cellsX = cellsX;
		_cellsY = cellsY;
		_minX = minX;
		_minY = minY;
		_cellWidth = cellWidth;
		_cellHeight = cellHeight;
	}

	public static NormalizedPointSet<TItem> Build(TItem[] source, Func<TItem, Location?> locationSelector)
	{
		var points = new PointD[source.Length];
		var validCount = 0;
		double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
		for (var i = 0; i < source.Length; i++)
		{
			if (locationSelector(source[i]) is not { } location)
			{
				points[i] = new PointD(double.NaN, double.NaN);
				continue;
			}
			var point = MapProjection.Default.LatLngToPoint(location);
			points[i] = point;
			validCount++;
			minX = Math.Min(minX, point.X);
			minY = Math.Min(minY, point.Y);
			maxX = Math.Max(maxX, point.X);
			maxY = Math.Max(maxY, point.Y);
		}

		if (validCount < GridPointCountThreshold)
			return new NormalizedPointSet<TItem>(source, points, null, null, 0, 0, 0, 0, 0, 0);

		// 1セルあたり平均1点程度を目安にセルを分割する。範囲が潰れている軸(全点同一座標など)は1セルにする
		var cells = (int)Math.Ceiling(Math.Sqrt(validCount));
		var width = maxX - minX;
		var height = maxY - minY;
		var cellsX = width > 0 ? cells : 1;
		var cellsY = height > 0 ? cells : 1;
		var cellWidth = width > 0 ? width / cellsX : 1;
		var cellHeight = height > 0 ? height / cellsY : 1;

		// counting sort の要領で2パスでセルごとの点インデックスを構築する
		var cellStarts = new int[cellsX * cellsY + 1];
		for (var i = 0; i < points.Length; i++)
		{
			if (double.IsNaN(points[i].X))
				continue;
			cellStarts[CellIndexOf(points[i], minX, minY, cellWidth, cellHeight, cellsX, cellsY) + 1]++;
		}
		for (var i = 1; i < cellStarts.Length; i++)
			cellStarts[i] += cellStarts[i - 1];

		var cellPointIndices = new int[validCount];
		var cursors = (int[])cellStarts.Clone();
		for (var i = 0; i < points.Length; i++)
		{
			if (double.IsNaN(points[i].X))
				continue;
			cellPointIndices[cursors[CellIndexOf(points[i], minX, minY, cellWidth, cellHeight, cellsX, cellsY)]++] = i;
		}

		return new NormalizedPointSet<TItem>(source, points, cellStarts, cellPointIndices, cellsX, cellsY, minX, minY, cellWidth, cellHeight);
	}

	private static int CellIndexOf(PointD point, double minX, double minY, double cellWidth, double cellHeight, int cellsX, int cellsY)
	{
		var cx = Math.Clamp((int)((point.X - minX) / cellWidth), 0, cellsX - 1);
		var cy = Math.Clamp((int)((point.Y - minY) / cellHeight), 0, cellsY - 1);
		return cy * cellsX + cx;
	}

	/// <summary>
	/// 指定した正規化座標から半径未満の距離で最も近い点のインデックスを返す。該当なしの場合は null
	/// </summary>
	public int? FindNearest(PointD query, double radius)
	{
		int? nearest = null;
		var nearestSq = radius * radius;

		if (_cellStarts is null || _cellPointIndices is null)
		{
			for (var i = 0; i < Points.Length; i++)
				UpdateNearest(i, query, ref nearest, ref nearestSq);
			return nearest;
		}

		var (cx0, cx1, cy0, cy1) = CellRange(query, radius);
		for (var cy = cy0; cy <= cy1; cy++)
			for (var cx = cx0; cx <= cx1; cx++)
			{
				var cell = cy * _cellsX + cx;
				for (var k = _cellStarts[cell]; k < _cellStarts[cell + 1]; k++)
					UpdateNearest(_cellPointIndices[k], query, ref nearest, ref nearestSq);
			}
		return nearest;
	}

	private void UpdateNearest(int index, PointD query, ref int? nearest, ref double nearestSq)
	{
		var dx = Points[index].X - query.X;
		var dy = Points[index].Y - query.Y;
		var distanceSq = dx * dx + dy * dy;
		// 座標なし(NaN)の点は比較が false になるため自然に除外される
		if (!(distanceSq < nearestSq))
			return;
		nearestSq = distanceSq;
		nearest = index;
	}

	/// <summary>
	/// 指定した正規化座標から半径未満の距離にある点を(インデックス, 正規化距離)で収集する。順序は不定
	/// </summary>
	public void CollectWithin(PointD query, double radius, List<(int Index, double Distance)> results)
	{
		var radiusSq = radius * radius;

		if (_cellStarts is null || _cellPointIndices is null)
		{
			for (var i = 0; i < Points.Length; i++)
				CollectIfWithin(i, query, radiusSq, results);
			return;
		}

		var (cx0, cx1, cy0, cy1) = CellRange(query, radius);
		for (var cy = cy0; cy <= cy1; cy++)
			for (var cx = cx0; cx <= cx1; cx++)
			{
				var cell = cy * _cellsX + cx;
				for (var k = _cellStarts[cell]; k < _cellStarts[cell + 1]; k++)
					CollectIfWithin(_cellPointIndices[k], query, radiusSq, results);
			}
	}

	private void CollectIfWithin(int index, PointD query, double radiusSq, List<(int Index, double Distance)> results)
	{
		var dx = Points[index].X - query.X;
		var dy = Points[index].Y - query.Y;
		var distanceSq = dx * dx + dy * dy;
		// 座標なし(NaN)の点は比較が false になるため自然に除外される
		if (!(distanceSq < radiusSq))
			return;
		results.Add((index, Math.Sqrt(distanceSq)));
	}

	/// <summary>
	/// クエリ円(中心±半径)と交差するセル範囲を求める。範囲外の値はクランプされるため、遠いセルの点は距離判定で除外される
	/// </summary>
	private (int Cx0, int Cx1, int Cy0, int Cy1) CellRange(PointD query, double radius)
		=> (
			Math.Clamp((int)((query.X - radius - _minX) / _cellWidth), 0, _cellsX - 1),
			Math.Clamp((int)((query.X + radius - _minX) / _cellWidth), 0, _cellsX - 1),
			Math.Clamp((int)((query.Y - radius - _minY) / _cellHeight), 0, _cellsY - 1),
			Math.Clamp((int)((query.Y + radius - _minY) / _cellHeight), 0, _cellsY - 1));
}
