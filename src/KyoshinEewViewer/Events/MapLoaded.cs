using KyoshinEewViewer.Map.Data;

namespace KyoshinEewViewer.Events;
public class MapLoaded
{
	public MapData Data { get; }

	public MapLoaded(MapData data)
	{
		Data = data;
	}
}
