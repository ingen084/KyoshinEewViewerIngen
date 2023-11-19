using MessagePack;
using System.Collections.Generic;

namespace TopoJsonConverter
{
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
		public int[][] Arcs { get; set; }
		[Key(1)]
		public int? AreaCode { get; set; }
	}

	[MessagePackObject]
	public struct DoubleVector(double x, double y)
	{
		[Key(0)]
		public double X { get; set; } = x;
		[Key(1)]
		public double Y { get; set; } = y;
	}
	[MessagePackObject]
	public struct FloatVector(float x, float y)
	{
		[Key(0)]
		public float X { get; set; } = x;
		[Key(1)]
		public float Y { get; set; } = y;
	}
	[MessagePackObject]
	public struct IntVector(int x, int y)
	{
		[Key(0)]
		public int X { get; set; } = x;
		[Key(1)]
		public int Y { get; set; } = y;
	}
}
