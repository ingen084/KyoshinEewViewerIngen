using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace TopoJsonConverter
{

	public class TopoJson
	{
		[DataMember(Name = "type")]
		public string Type { get; set; }
		[DataMember(Name = "transform")]
		public TopoJsonTransform Transform { get; set; }
		[DataMember(Name = "objects")]
		public Dictionary<string, TopoJsonGeometry> Objects { get; set; }
		[DataMember(Name = "arcs")]
		public int[][][] Arcs { get; set; }

		public IntVector[][] GetArcs()
			=> Arcs.Select(a1 => a1.Select(a2 => new IntVector(a2[1], a2[0])).ToArray()).ToArray();
	}

	public class TopoJsonTransform
	{
		[DataMember(Name = "scale")]
		public double[] Scale { get; set; }
		[DataMember(Name = "translate")]
		public double[] Translate { get; set; }
	}

	public class TopoJsonGeometry
	{
		[DataMember(Name = "type")]
		public TopoJsonGeometryType Type { get; set; }
		//[DataMember(Name = "properties")]
		//public Dictionary<string, string> Properties { get; set; }
		[DataMember(Name = "arcs")]
		public JArray Arcs { get; set; }
		public int[][] GetPolygonArcs() => Arcs.Cast<JArray>().Select(a => a.Children<JValue>().Select(v => (int)v).ToArray()).ToArray();
		public int[][][] GetMultiPolygonArcs() => Arcs.Cast<JArray>().Select(a => a.Children<JArray>().Select(b => b.Children<JValue>().Select(v => (int)v).ToArray()).ToArray()).ToArray();

		[DataMember(Name = "id")]
		public int Id { get; set; }
		[DataMember(Name = "geometries")]
		public TopoJsonGeometry[] Geometries { get; set; }
	}
	public enum TopoJsonGeometryType
	{
		Polygon,
		MultiPolygon,
		GeometryCollection,
	}
}
