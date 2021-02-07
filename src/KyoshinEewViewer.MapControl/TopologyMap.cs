using MessagePack;
using System.IO;

namespace KyoshinEewViewer.MapControl
{
	[MessagePackObject]
	public class TopologyMap
	{
		[Key(0)]
		public DoubleVector Scale { get; set; }
		[Key(1)]
		public DoubleVector Translate { get; set; }
		[Key(2)]
		public TopologyPolygon[] Polygons { get; set; }
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
		public TopologyArcType Type { get; set; }
	}
	public enum TopologyArcType
	{
		Coastline = 0,
		Admin,
		Area,
	}
	[MessagePackObject]
	public class TopologyPolygon
	{
		[Key(0)]
		public int[] Arcs { get; set; }
		[Key(1)]
		public int? AreaCode { get; set; }
		[Key(2)]
		public string CountryCode { get; set; }
		//[Key(2)]
		//public string Prefecture { get; set; }
		//[Key(3)]
		//public string SubPrefecture { get; set; }
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
