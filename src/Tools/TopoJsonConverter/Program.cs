﻿using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TopoJsonConverter
{
	class Program
	{
		static ConcurrentDictionary<LandLayerType, Dictionary<int, FloatVector>> CenterLocations { get; } = new();

		static async Task Main(string[] args)
		{
			Console.Write("topojsonが入ったフォルダ: ");
			var path = Console.ReadLine();
			var topologyMaps = new ConcurrentDictionary<LandLayerType, TopologyMap>();

			Console.Write("マップデータの保存先: ");
			var outPath = Console.ReadLine();

			await Task.WhenAll(Directory.GetFiles(path, "*.json").Select(file => Task.Run(() =>
			{
				var ntype = Path.GetFileNameWithoutExtension(file).ToLandLayerType();
				if (ntype is not LandLayerType type)
				{
					Console.WriteLine(Path.GetFileName(file) + " のレイヤーの種類がわかりませんでした。");
					return;
				}
				var json = JsonConvert.DeserializeObject<TopoJson>(File.ReadAllText(file));
				topologyMaps[type] = CreateMap(json, type);
			})).ToArray());

			Console.WriteLine("データを出力しています");
			using (var file = File.OpenWrite(outPath))
				MessagePackSerializer.Serialize(file, topologyMaps, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			Console.WriteLine("中心座標データを出力しています");
			using (var file = File.OpenWrite(Path.Combine(Path.GetDirectoryName(outPath), Path.GetFileNameWithoutExtension(outPath) + "_center" + Path.GetExtension(outPath))))
				MessagePackSerializer.Serialize(file, CenterLocations, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

			Console.WriteLine("completed!");
		}

		private static TopologyMap CreateMap(TopoJson json, LandLayerType layerType)
		{
			var result = new TopologyMap
			{
				Scale = new DoubleVector(json.Transform.Scale[1], json.Transform.Scale[0]),
				Translate = new DoubleVector(json.Transform.Translate[1], json.Transform.Translate[0]),
			};

			Console.WriteLine(layerType + " ポリゴンを処理しています...");
			result.Polygons = new List<TopologyPolygon>();
			// TODO: 穴開きポリゴンの実装をする
			foreach (var obj in json.Objects.Values)
				foreach (var geo in obj.Geometries)
				{
					switch (geo.Type)
					{
						case TopoJsonGeometryType.Polygon:
							{
								var arcs = geo.GetPolygonArcs();
								result.Polygons.Add(new TopologyPolygon
								{
									Arcs = arcs,
									AreaCode =
										geo.Properties.ContainsKey("code") && int.TryParse(geo.Properties["code"], out var c)
											? c
											: geo.Properties.ContainsKey("regioncode") && int.TryParse(geo.Properties["regioncode"], out var c2)
												? c2
												: geo.Properties.ContainsKey("ISO_N3") && int.TryParse(geo.Properties["ISO_N3"], out var n)
													? n
													: null,
								});
							}
							break;
						case TopoJsonGeometryType.LineString when layerType == LandLayerType.TsunamiForecastArea:
							{
								var arc = geo.GetPolygonArc();
								result.Polygons.Add(new TopologyPolygon
								{
									Arcs = new[] { arc },
									AreaCode =
										geo.Properties.ContainsKey("code") && int.TryParse(geo.Properties["code"], out var c)
											? c
											: null,
								});
							}
							break;
						case TopoJsonGeometryType.MultiPolygon:
							foreach (var arcs in geo.GetMultiPolygonArcs())
								result.Polygons.Add(new TopologyPolygon
								{
									Arcs = arcs,
									AreaCode =
										geo.Properties.ContainsKey("code") && int.TryParse(geo.Properties["code"], out var c)
											? c
											: geo.Properties.ContainsKey("regioncode") && int.TryParse(geo.Properties["regioncode"], out var c2)
												? c2
												: geo.Properties.ContainsKey("ISO_N3") && int.TryParse(geo.Properties["ISO_N3"], out var n)
													? n
													: null,
								});
							break;
						case TopoJsonGeometryType.MultiLineString when layerType == LandLayerType.TsunamiForecastArea:
							foreach (var arc in geo.GetPolygonArcs())
								result.Polygons.Add(new TopologyPolygon
								{
									Arcs = new[] { arc },
									AreaCode =
										geo.Properties.ContainsKey("code") && int.TryParse(geo.Properties["code"], out var c)
											? c
											: null,
								});
							break;
					}
				}
			Console.WriteLine(layerType + " " + result.Polygons.Count + "個のポリゴンが処理されました");

			Console.WriteLine(layerType + " 境界線を処理しています...");
			// 海岸線の判定を先にやっておく
			result.Arcs = json.GetArcs().Select((a, index) =>
			{
				var ta = new TopologyArc { Arc = a };
				// 該当するPolyLineを使用しているポリゴンを取得
				var refPolygons = result.Polygons.Where(p => p.Arcs[0].Any(i => (i < 0 ? Math.Abs(i) - 1 : i) == index)).ToArray();

				// 1つしか存在しなければそいつは海岸線
				if (refPolygons.Length <= 1)
					ta.Type = TopologyArcType.Coastline;
				// ポリゴン自体が結合不可もしくは使用しているポリゴンがAreaCodeがnullでないかつ上3桁が違うものであれば県境
				else if (layerType.GetMultiareaGroupNo() == 1 || refPolygons.Where(p => p.AreaCode != null).GroupBy(p => p.AreaCode / layerType.GetMultiareaGroupNo()).Count() > 1)
					ta.Type = TopologyArcType.Admin;
				// そうでもないなら一次細分区域
				else
					ta.Type = TopologyArcType.Area;
				return ta;
			}).ToArray();
			Console.WriteLine(layerType + " " + result.Arcs.Length + "個の境界線を処理しました");

			// 以下のレイヤーに対してはポリゴンの中心点を計算しない
			if (new[]
			{
				LandLayerType.NationalAndRegionForecastArea,
				LandLayerType.TsunamiForecastArea,
				LandLayerType.WorldWithoutJapan,
			}.Contains(layerType))
				return result;

			Console.WriteLine(layerType + " ポリゴンの中心点を計算しています...");

			var centerPoints = new Dictionary<int, FloatVector>();

			// ポリゴン単体
			foreach (var g in result.Polygons.GroupBy(p => p.AreaCode))
				CalcCenterLocation(g);
			if (layerType.GetMultiareaGroupNo() != 1)
			{
				// 広域範囲
				foreach (var g in result.Polygons.GroupBy(p => p.AreaCode / layerType.GetMultiareaGroupNo()))
					CalcCenterLocation(g);
			}

			// ポリゴン郡からバウンドボックスを求め、その中心座標を取得する
			void CalcCenterLocation(IGrouping<int?, TopologyPolygon> g)
			{
				if (g.Key is null)
					return;
				// バウンドボックスを求める
				var minLoc = new DoubleVector(float.MaxValue, float.MaxValue);
				var maxLoc = new DoubleVector(float.MinValue, float.MinValue);
				foreach (var p in g)
				{
					// バウンドボックスを求めるために地理座標の計算をしておく
					var points = new List<DoubleVector>();
					foreach (var i in p.Arcs[0])
					{
						if (points.Count == 0)
						{
							if (i < 0)
								points.AddRange(result.Arcs[Math.Abs(i) - 1].Arc.ToLocations(result).Reverse());
							else
								points.AddRange(result.Arcs[i].Arc.ToLocations(result));
							continue;
						}

						if (i < 0)
							points.AddRange(result.Arcs[Math.Abs(i) - 1].Arc.ToLocations(result).Reverse().Skip(1));
						else
							points.AddRange(result.Arcs[i].Arc.ToLocations(result)[1..]);
					}
					foreach (var l in points)
					{
						minLoc.X = Math.Min(minLoc.X, l.X);
						minLoc.Y = Math.Min(minLoc.Y, l.Y);

						maxLoc.X = Math.Max(maxLoc.X, l.X);
						maxLoc.Y = Math.Max(maxLoc.Y, l.Y);
					}
				}
				//var center = new DoubleVector(minLoc.X + (maxLoc.X - minLoc.X) / 2, minLoc.Y + (maxLoc.Y - minLoc.Y) / 2);
				centerPoints[g.Key.Value] = new FloatVector((float)(minLoc.X + (maxLoc.X - minLoc.X) / 2), (float)(minLoc.Y + (maxLoc.Y - minLoc.Y) / 2));
				//new IntVector((int)((center.X - result.Translate.X) / result.Scale.X), (int)((center.Y - result.Translate.Y) / result.Scale.Y));
			}
			CenterLocations[layerType] = centerPoints;
			return result;
		}
	}
}
