using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Disassemblers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Properties;
using SkiaSharp;
using System.Diagnostics;

BenchmarkRunner.Run<GeneratePolygon>();

public class GeneratePolygon
{
	private TopologyMap Map { get; }
	private PolygonFeature[] PolyFeatures { get; }
	private PolylineFeature[] LineFeatures { get; }

	[Params(4, 6, 8, 10, 12)]
	public int Zoom { get; set; }

	public GeneratePolygon()
	{
		Map = TopologyMap.LoadCollection(Resources.DefaultMap)[LandLayerType.MunicipalityEarthquakeTsunamiArea];
		PolyFeatures = new PolygonFeature[Map.Polygons?.Length ?? 0];
		LineFeatures = new PolylineFeature[Map.Arcs?.Length ?? 0];

		if (Map.Arcs != null)
			for (var i = 0; i < Map.Arcs.Length; i++)
				LineFeatures[i] = new PolylineFeature(Map, i);

		if (Map.Polygons != null)
			for (var i = 0; i < Map.Polygons.Length; i++)
				PolyFeatures[i] = new PolygonFeature(Map, LineFeatures, Map.Polygons[i]);
	}

	[Benchmark]
	public void CreatePolyFeatures()
	{
		foreach (var p in PolyFeatures)
			p.GetOrCreatePath(Zoom);
	}

	[Benchmark]
	public void CreateLineFeatures()
	{
		foreach (var t in LineFeatures)
			t.GetOrCreatePath(Zoom);
	}

	[IterationCleanup]
	public void Cleanup()
	{
		foreach (var p in PolyFeatures)
			p.ClearCache();
		foreach (var p in LineFeatures)
			p.ClearCache();
	}
}

//[SimpleJob(RuntimeMoniker.Net70)]
//[SimpleJob(RuntimeMoniker.NativeAot70)]
public class LoadData
{
	private TopologyMap Map { get; }

	public LoadData()
	{
		Map = TopologyMap.LoadCollection(Resources.DefaultMap)[LandLayerType.MunicipalityEarthquakeTsunamiArea];
	}

	[Benchmark]
	public void CurrentLogic()
	{
		var polyFeatures = new List<PolygonFeature>();
		var lineFeatures = new List<PolylineFeature>();

		if (Map.Arcs != null)
			for (var i = 0; i < Map.Arcs.Length; i++)
				lineFeatures.Add(new PolylineFeature(Map, i));

		if (Map.Polygons != null)
			foreach (var i in Map.Polygons)
				polyFeatures.Add(new PolygonFeature(Map, lineFeatures.ToArray(), i));

		_ = polyFeatures.ToArray();
	}

	[Benchmark]
	public void SetCapacity()
	{
		var polyFeatures = new PolygonFeature[Map.Polygons?.Length ?? 0];
		var lineFeatures = new PolylineFeature[Map.Arcs?.Length ?? 0];

		if (Map.Arcs != null)
			for (var i = 0; i < Map.Arcs.Length; i++)
				lineFeatures[i] = new PolylineFeature(Map, i);

		if (Map.Polygons != null)
			for (var i = 0; i < Map.Polygons.Length; i++)
				polyFeatures[i] = new PolygonFeature(Map, lineFeatures, Map.Polygons[i]);
	}
}
