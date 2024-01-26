using MessagePack;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace KyoshinEewViewer.Map;

[MessagePackObject]
public class TopologyMap
{
	[Key(0)]
	public PointD Scale { get; set; }
	[Key(1)]
	public PointD Translate { get; set; }
	[Key(2)]
	public TopologyPolygon[]? Polygons { get; set; }
	[Key(3)]
	public TopologyArc[]? Arcs { get; set; }
	[Key(4)]
	public Dictionary<int, IntVector>? CenterPoints { get; set; }

	// TODO: やっつけ実装感ある…ここに置くべきではない
	public event Action<int>? AsyncObjectGenerated;
	internal void OnAsyncObjectGenerated(int zoom) => AsyncObjectGenerated?.Invoke(zoom);

	public static TopologyMap Load(byte[] data)
		=> MessagePackSerializer.Deserialize<TopologyMap>(data, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray).WithResolver(GeneratedMessagePackResolver.InstanceWithStandardAotResolver));
	public static IImmutableDictionary<int, TopologyMap> LoadCollection(Stream stream)
		=> MessagePackSerializer.Deserialize<IImmutableDictionary<int, TopologyMap>>(stream, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray).WithResolver(GeneratedMessagePackResolver.InstanceWithStandardAotResolver));
}
[MessagePackObject]
public class TopologyArc
{
	[Key(0)]
	public IntVector[]? Arc { get; set; }
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
	public int[][]? Arcs { get; set; }
	[Key(1)]
	public int? Code { get; set; }
}
[MessagePackObject]
public struct IntVector(int x, int y)
{
	[Key(0)]
	public int X { get; } = x;
	[Key(1)]
	public int Y { get; } = y;
}
