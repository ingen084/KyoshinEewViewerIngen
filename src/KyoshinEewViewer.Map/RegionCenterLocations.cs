using KyoshinMonitorLib;
using MessagePack;
using System.Collections.Immutable;

namespace KyoshinEewViewer.Map;

public class RegionCenterLocations
{
	private static RegionCenterLocations? _default;
	public static RegionCenterLocations Default => _default ??= new RegionCenterLocations();

	private RegionCenterLocations()
	{
		CenterLocations = MessagePackSerializer.Deserialize<IImmutableDictionary<int, IImmutableDictionary<int, FloatVector>>>(
			Properties.Resources.CenterLocations,
			MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)
				.WithResolver(GeneratedMessagePackResolver.InstanceWithStandardAotResolver)
		);
	}

	public Location? GetLocation(LandLayerType layerType, int code)
	{
		if (!CenterLocations.TryGetValue((int)layerType, out var dic))
			return null;
		if (!dic.TryGetValue(code, out var location))
			return null;
		return new Location(location.X, location.Y);
	}

	private IImmutableDictionary<int, IImmutableDictionary<int, FloatVector>> CenterLocations { get; }
}

[MessagePackObject]
public struct FloatVector(float x, float y)
{
	[Key(0)]
	public float X { get; set; } = x;
	[Key(1)]
	public float Y { get; set; } = y;
}
