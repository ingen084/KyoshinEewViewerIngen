using Avalonia.Platform;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Series.Qzss.Services;

/// <summary>
/// 指定河川洪水予報の対象河川の形状
/// </summary>
/// <remarks>
/// 気象庁が公開している GeoJSON から生成したもの
/// https://www.jma.go.jp/bosai/jmatile/data/map/none/none/none/surf/designated_river/data.geojson
/// (出典: 気象庁ホームページ / 公共データ利用規約 第1.0版)
/// </remarks>
public static class FloodForecastRiverService
{
	private static readonly Lazy<Dictionary<long, Location[][]>> Rivers
		= new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

	/// <summary>
	/// 洪水予報区のコードから河川の形状を取得する
	/// </summary>
	/// <param name="code">洪水予報区のコード</param>
	/// <param name="parts">河川の形状。分岐や中断があるため複数の線分になる</param>
	public static bool TryGetRiver(long code, out Location[][] parts)
		=> Rivers.Value.TryGetValue(code, out parts!);

	private static Dictionary<long, Location[][]> Load()
	{
		using var stream = AssetLoader.Open(new Uri("avares://KyoshinEewViewer/Assets/DCRFloodForecastRiver.geojson.gz", UriKind.Absolute))
			?? throw new Exception("洪水予報の河川データが読み込めません");
		using var gzip = new GZipStream(stream, CompressionMode.Decompress);
		using var json = JsonDocument.Parse(gzip);

		var result = new Dictionary<long, Location[][]>();
		foreach (var feature in json.RootElement.GetProperty("features").EnumerateArray())
		{
			var code = long.Parse(
				feature.GetProperty("properties").GetProperty("riverCode").GetString()
				?? throw new Exception("riverCode が取得できません"));

			var lines = feature.GetProperty("geometry").GetProperty("coordinates");
			var parts = new Location[lines.GetArrayLength()][];
			var partIndex = 0;
			foreach (var line in lines.EnumerateArray())
			{
				var points = new Location[line.GetArrayLength()];
				var pointIndex = 0;
				foreach (var point in line.EnumerateArray())
					// GeoJSON の座標は 経度, 緯度 の順
					points[pointIndex++] = new Location(point[1].GetSingle(), point[0].GetSingle());
				parts[partIndex++] = points;
			}
			result[code] = parts;
		}
		return result;
	}
}
