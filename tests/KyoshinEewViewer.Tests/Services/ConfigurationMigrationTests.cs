using KyoshinEewViewer.Core.Models;
using System.Text.Json;

namespace KyoshinEewViewer.Tests.Services;

public class ConfigurationMigrationTests
{
	[Fact(DisplayName = "旧名称の地点予測取り込み設定は新しい設定へ引き継がれ、保存時には旧名称で出力されない")]
	public void 旧名称の地点予測取り込み設定は新しい設定へ引き継がれる()
	{
		var config = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>("""{"Eew":{"EnableExternalPointForecast":true}}""");
		Assert.NotNull(config);
		Assert.True(config.Eew.EnableWebhookPointForecast);

		var json = JsonSerializer.Serialize(config);
		Assert.DoesNotContain("EnableExternalPointForecast", json);
		Assert.Contains("\"EnableWebhookPointForecast\":true", json);

		// 新しい名称で保存された設定はそのまま読み込める
		var saved = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(json);
		Assert.True(saved?.Eew.EnableWebhookPointForecast);
	}
}
