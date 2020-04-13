using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
				Arcs = json.GetArcs(),
			};

			result.Polygons = new List<int[]>();
			// 穴あきポリゴンは実装しません！
			foreach (var obj in json.Objects.Values)
				foreach (var geo in obj.Geometries)
				{
					switch (geo.Type)
					{
						case TopoJsonGeometryType.Polygon:
							{
								var arcs = geo.GetPolygonArcs();
								result.Polygons.Add(arcs[0]);
							}
							break;
						case TopoJsonGeometryType.MultiPolygon:
							foreach (var arcs in geo.GetMultiPolygonArcs())
								result.Polygons.Add(arcs[0]);
							break;
					}
				}

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
		public List<int[]> Polygons { get; set; }
		[Key(3)]
		public IntVector[][] Arcs { get; set; }
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
