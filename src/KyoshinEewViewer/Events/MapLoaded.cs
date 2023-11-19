using KyoshinEewViewer.Map.Data;

namespace KyoshinEewViewer.Events;
public class MapLoaded(MapData data)
{
	public MapData Data { get; } = data;
}
