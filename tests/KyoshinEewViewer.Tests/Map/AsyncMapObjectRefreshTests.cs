using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;

namespace KyoshinEewViewer.Tests.Map;

public class AsyncMapObjectRefreshTests
{
	/// <summary>
	/// ホストに1度描画を行わせ、描画パラメータ(ズーム)を記録させる
	/// </summary>
	private static void Render(MapLayerHost host, double zoom)
	{
		using var recorder = new SKPictureRecorder();
		var canvas = recorder.BeginRecording(new SKRect(0, 0, 100, 100));
		host.Render(canvas, new LayerRenderParameter { Zoom = zoom }, false);
		using var picture = recorder.EndRecording();
	}

	[Fact(DisplayName = "共有された地形レイヤーの非同期生成完了は該当ズームを描画中のホストのみを更新する")]
	public void 共有された地形レイヤーの非同期生成完了は該当ズームを描画中のホストのみを更新する()
	{
		var map = new MapData();
		var landLayer = new LandLayer { Map = map };
		using var mainMapHost = new MapLayerHost { Layers = [landLayer] };
		using var miniMapHost = new MapLayerHost { Layers = [landLayer] };
		Render(mainMapHost, 8);
		Render(miniMapHost, 5);
		var mainMapRefreshCount = 0;
		var miniMapRefreshCount = 0;
		mainMapHost.RefreshRequested += () => mainMapRefreshCount++;
		miniMapHost.RefreshRequested += () => miniMapRefreshCount++;

		// ミニマップ側のズームの生成完了ではメインマップの記録済みピクチャを無効化しない
		map.OnAsyncObjectGenerated(LandLayerType.WorldWithoutJapan, 5);
		Assert.Equal(0, mainMapRefreshCount);
		Assert.Equal(1, miniMapRefreshCount);

		// メインマップ側のズームの生成完了ではミニマップを無効化しない
		map.OnAsyncObjectGenerated(LandLayerType.WorldWithoutJapan, 8);
		Assert.Equal(1, mainMapRefreshCount);
		Assert.Equal(1, miniMapRefreshCount);
	}

	[Fact(DisplayName = "一度も描画していないホストは非同期生成完了時に安全側に倒して更新する")]
	public void 一度も描画していないホストは非同期生成完了時に安全側に倒して更新する()
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

	[Fact(DisplayName = "共有された境界線レイヤーの非同期生成完了も該当ズームを描画中のホストのみを更新する")]
	public void 共有された境界線レイヤーの非同期生成完了も該当ズームを描画中のホストのみを更新する()
	{
		var map = new MapData();
		var borderLayer = new LandBorderLayer { Map = map };
		using var mainMapHost = new MapLayerHost { Layers = [borderLayer] };
		using var miniMapHost = new MapLayerHost { Layers = [borderLayer] };
		Render(mainMapHost, 8);
		Render(miniMapHost, 7);
		var mainMapRefreshCount = 0;
		var miniMapRefreshCount = 0;
		mainMapHost.RefreshRequested += () => mainMapRefreshCount++;
		miniMapHost.RefreshRequested += () => miniMapRefreshCount++;

		map.OnAsyncObjectGenerated(LandLayerType.EarthquakeInformationSubdivisionArea, 7);

		Assert.Equal(0, mainMapRefreshCount);
		Assert.Equal(1, miniMapRefreshCount);
	}
}
