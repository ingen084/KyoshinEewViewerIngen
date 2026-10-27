using Sentry;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer.Tests.Services;

public class ProcessResourceRecorderTests
{
	[Fact(DisplayName = "送信するイベントに、プロセスの資源の使用量と直近1日分の推移が添付される")]
	public void AttachAddsCurrentUsageAndRecentHistory()
	{
		for (var i = 0; i < ProcessResourceRecorder.MaxRecords + 6; i++)
			ProcessResourceRecorder.Record();

		var contexts = Serialize(ProcessResourceRecorder.Attach(new SentryEvent())).GetProperty("contexts");

		var current = contexts.GetProperty("process_resources");
		Assert.True(current.GetProperty("private_bytes").GetInt64() > 0);
		Assert.True(current.GetProperty("working_set_bytes").GetInt64() > 0);
		Assert.True(current.GetProperty("handles").GetInt64() > 0);
		if (OperatingSystem.IsWindows())
		{
			// システム全体で不足したのかを判断するための値
			Assert.True(current.GetProperty("commit_limit_bytes").GetInt64() > 0);
			Assert.True(current.GetProperty("commit_available_bytes").GetInt64() > 0);
			Assert.True(current.GetProperty("gdi_objects").GetInt64() >= 0);
			Assert.True(current.GetProperty("user_objects").GetInt64() >= 0);
		}

		var samples = contexts.GetProperty("process_resources_history").GetProperty("samples");
		Assert.Equal(ProcessResourceRecorder.MaxRecords, samples.GetArrayLength());
		Assert.True(samples[0].GetProperty("private_bytes").GetInt64() > 0);
		Assert.True(samples[0].GetProperty("threads").GetInt64() > 0);
	}

	private static JsonElement Serialize(SentryEvent sentryEvent)
	{
		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream))
			sentryEvent.WriteTo(writer, null);
		return JsonDocument.Parse(stream.ToArray()).RootElement;
	}
}
