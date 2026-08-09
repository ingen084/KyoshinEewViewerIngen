using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;

namespace KyoshinEewViewer.Tests.Map;

public class MapLayerHostLifecycleTests
{
	private sealed class TestLayer : MapLayer
	{
		public override bool NeedPersistentUpdate => false;
		public override void RefreshResourceCache(WindowTheme targetControl) { }
		public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating) { }
		public void RequestRefresh() => RefreshRequest();
	}

	[Fact(DisplayName = "描画ホストを繰り返し休止・再開しても共有レイヤーの購読は重複しない")]
	public void 描画ホストを繰り返し休止再開しても共有レイヤーの購読は重複しない()
	{
		var layer = new TestLayer();
		using var host = new MapLayerHost { Layers = [layer] };
		var refreshCount = 0;
		host.RefreshRequested += () => refreshCount++;

		layer.RequestRefresh();
		Assert.Equal(1, refreshCount);

		for (var i = 0; i < 20; i++)
		{
			host.Deactivate();
			host.Deactivate();
			Assert.False(host.IsActive);
			layer.RequestRefresh();
			Assert.Equal(1 + (i * 2), refreshCount);

			host.Activate();
			host.Activate();
			Assert.True(host.IsActive);
			Assert.Equal(2 + (i * 2), refreshCount);
			layer.RequestRefresh();
			Assert.Equal(3 + (i * 2), refreshCount);
		}
	}

	[Fact(DisplayName = "描画ホストの休止中に設定したレイヤーは再開時に購読される")]
	public void 描画ホストの休止中に設定したレイヤーは再開時に購読される()
	{
		var layer = new TestLayer();
		using var host = new MapLayerHost();
		var refreshCount = 0;
		host.RefreshRequested += () => refreshCount++;

		host.Deactivate();
		host.Layers = [layer];
		layer.RequestRefresh();
		Assert.Equal(0, refreshCount);

		host.Activate();
		Assert.Equal(1, refreshCount);
		layer.RequestRefresh();
		Assert.Equal(2, refreshCount);
	}

	[Fact(DisplayName = "描画ホストの破棄後は共有レイヤーの更新通知を受けない")]
	public void 描画ホストの破棄後は共有レイヤーの更新通知を受けない()
	{
		var layer = new TestLayer();
		var host = new MapLayerHost { Layers = [layer] };
		var refreshCount = 0;
		host.RefreshRequested += () => refreshCount++;

		host.Dispose();
		layer.RequestRefresh();

		Assert.False(host.IsActive);
		Assert.Equal(0, refreshCount);
		Assert.Throws<ObjectDisposedException>(host.Activate);
	}
}
