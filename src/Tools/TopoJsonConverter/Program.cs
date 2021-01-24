using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TopoJsonConverter
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Write("topojson file path: ");
			var path = Console.ReadLine();
			var json = JsonConvert.DeserializeObject<TopoJson>(File.ReadAllText(path));

			Console.Write("save file path: ");
			var outPath = Console.ReadLine();

			var result = new TopologyMap
			{
				Scale = new DoubleVector(json.Transform.Scale[1], json.Transform.Scale[0]),
				Translate = new DoubleVector(json.Transform.Translate[1], json.Transform.Translate[0]),
			};

			result.Polygons = new List<TopologyPolygon>();
			// 穴あきポリゴンは実装しません！
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
									Arcs = arcs[0],
									CountryCode = geo.Properties["POSTAL"],
									Prefecture = geo.Properties["N03_001"],
									SubPrefecture = geo.Properties["N03_002"],
									//SubPrefecture2 = geo.Properties["N03_003"],
									//City = geo.Properties["N03_004"],
									//AdministrativeAreaCode = int.TryParse(geo.Properties["N03_007"], out var c) ? c : 0,
								});
							}
							break;
						case TopoJsonGeometryType.MultiPolygon:
							foreach (var arcs in geo.GetMultiPolygonArcs())
								result.Polygons.Add(new TopologyPolygon
								{
									Arcs = arcs[0],
									CountryCode = geo.Properties["POSTAL"],
									Prefecture = geo.Properties["N03_001"],
									SubPrefecture = geo.Properties["N03_002"],
									//SubPrefecture2 = geo.Properties["N03_003"],
									//City = geo.Properties["N03_004"],
									//AdministrativeAreaCode = int.TryParse(geo.Properties["N03_007"], out var c) ? c : 0,
								});
							break;
					}
				}

			// 海岸線の判定を先にやっておく
			result.Arcs = json.GetArcs().Select((a, index) => new TopologyArc
			{
				Arc = a,
				IsCoastline = result.Polygons.Count(p => p.Arcs.Any(i => (i < 0 ? Math.Abs(i) - 1 : i) == index)) <= 1,
			}).ToArray();

			using (var file = File.OpenWrite(outPath))
				MessagePackSerializer.Serialize(file, result, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

			Console.WriteLine("completed!");
		}
	}

	[MessagePackObject]
	public class TopologyMap
	{
		[Key(0)]
		public DoubleVector Scale { get; set; }
		[Key(1)]
		public DoubleVector Translate { get; set; }
		[Key(2)]
		public List<TopologyPolygon> Polygons { get; set; }
		[Key(3)]
		public TopologyArc[] Arcs { get; set; }

		public static TopologyMap Load(string path)
		{
			using var file = File.OpenRead(path);
			return MessagePackSerializer.Deserialize<TopologyMap>(file, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
		}
	}
	[MessagePackObject]
	public class TopologyArc
	{
		[Key(0)]
		public IntVector[] Arc { get; set; }
		[Key(1)]
		public bool IsCoastline { get; set; }
	}
	[MessagePackObject]
	public class TopologyPolygon
	{
		[Key(0)]
		public int[] Arcs { get; set; }
		[Key(1)]
		public string CountryCode { get; set; }
		[Key(2)]
		public string Prefecture { get; set; }
		[Key(3)]
		public string SubPrefecture { get; set; }
		//[Key(4)]
		//public string SubPrefecture2 { get; set; }
		//[Key(5)]
		//public string City { get; set; }
		//[Key(6)]
		//public int AdministrativeAreaCode { get; set; }
	}

	[MessagePackObject]
	public struct DoubleVector
	{
		public DoubleVector(double x, double y)
		{
			X = x;
			Y = y;
		}

		[Key(0)]
		public double X { get; }
		[Key(1)]
		public double Y { get; }
	}
	[MessagePackObject]
	public struct IntVector
	{
		public IntVector(int x, int y)
		{
			X = x;
			Y = y;
		}

		[Key(0)]
		public int X { get; }
		[Key(1)]
		public int Y { get; }
	}
}
