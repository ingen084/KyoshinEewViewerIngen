using KyoshinEewViewer.Map.Properties;
using KyoshinMonitorLib;
using MessagePack;
using System.Collections.Immutable;
using System.IO;

namespace KyoshinEewViewer.Map;

public class RegionCenterLocations
{
	private static RegionCenterLocations? _default;
	public static RegionCenterLocations Default => _default ??= new RegionCenterLocations();

	private RegionCenterLocations()
	{
		using var centerLocationStream = new MemoryStream(Resources.world_center_mpk);
		CenterLocations = MessagePackSerializer.Deserialize<IImmutableDictionary<int, IImmutableDictionary<long, FloatVector>>>(
			centerLocationStream,
			MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)
		);
	}

	public Location? GetLocation(LandLayerType layerType, long code)
	{
		if (!CenterLocations.TryGetValue((int)layerType, out var dic))
			return null;
		if (!dic.TryGetValue(code, out var location))
			return null;
		return new Location(location.X, location.Y);
	}

	private IImmutableDictionary<int, IImmutableDictionary<long, FloatVector>> CenterLocations { get; }
}

[MessagePackObject]
public struct FloatVector(float x, float y)
{
	[Key(0)]
	public float X { get; set; } = x;
	[Key(1)]
	public float Y { get; set; } = y;
}
