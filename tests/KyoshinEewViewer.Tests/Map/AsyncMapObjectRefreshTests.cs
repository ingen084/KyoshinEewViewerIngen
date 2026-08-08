using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;

namespace KyoshinEewViewer.Tests.Map;

public class AsyncMapObjectRefreshTests
{
	[Fact(DisplayName = "共有された地形レイヤーは非同期生成完了時にすべての描画ホストを更新する")]
	public void 共有された地形レイヤーは非同期生成完了時にすべての描画ホストを更新する()
	{
		var map = new MapData();
		var landLayer = new LandLayer { Map = map };
		using var mainMapHost = new MapLayerHost { Layers = [landLayer] };
		using var miniMapHost = new MapLayerHost { Layers = [landLayer] };
		var mainMapRefreshCount = 0;
		var miniMapRefreshCount = 0;
		mainMapHost.RefreshRequested += () => mainMapRefreshCount++;
		miniMapHost.RefreshRequested += () => miniMapRefreshCount++;

		map.OnAsyncObjectGenerated(LandLayerType.WorldWithoutJapan, 7);

		Assert.Equal(1, mainMapRefreshCount);
		Assert.Equal(1, miniMapRefreshCount);
	}

	[Fact(DisplayName = "共有された境界線レイヤーは別ズームの非同期生成完了時にもすべての描画ホストを更新する")]
	public void 共有された境界線レイヤーは別ズームの非同期生成完了時にもすべての描画ホストを更新する()
	{
		var map = new MapData();
		var borderLayer = new LandBorderLayer { Map = map };
		using var mainMapHost = new MapLayerHost { Layers = [borderLayer] };
		using var miniMapHost = new MapLayerHost { Layers = [borderLayer] };
		var mainMapRefreshCount = 0;
		var miniMapRefreshCount = 0;
		mainMapHost.RefreshRequested += () => mainMapRefreshCount++;
		miniMapHost.RefreshRequested += () => miniMapRefreshCount++;

		map.OnAsyncObjectGenerated(LandLayerType.EarthquakeInformationSubdivisionArea, 7);

		Assert.Equal(1, mainMapRefreshCount);
		Assert.Equal(1, miniMapRefreshCount);
	}
}
