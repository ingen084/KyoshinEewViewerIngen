using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Voicevox.Model;
using ManagedBass;
using Splat;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace KyoshinEewViewer.Services;

public class VoicevoxService
{
	private KyoshinEewViewerConfiguration Config { get; }

	private HttpClient HttpClient { get; } = new();

	public VoicevoxService(KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<VoicevoxService>();

		Config = config;
		HttpClient.BaseAddress = new Uri("http://localhost:50021/");
	}

	public async Task<Speaker[]> GetSpeakers()
	{
		using var response = await HttpClient.GetAsync("speakers");
		var body = await response.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<Speaker[]>(body);
	}

	public async Task PlayTest()
	{
		var c = HttpUtility.ParseQueryString(string.Empty);
		c.Add("text", $"これは読み上げのテストです。現在の時刻は、{DateTime.Now:H時m分s秒}です");
		c.Add("speaker", "9"); // 74

		using var response = await HttpClient.PostAsync($"audio_query?{c}", null);
		var querybody = await JsonSerializer.DeserializeAsync<AudioQuery>(await response.Content.ReadAsStreamAsync());
		if (querybody is null)
			return;

		querybody.SpeedScale = Config.Voicevox.SpeedScale;
		querybody.PitchScale = Config.Voicevox.PitchScale;
		querybody.IntonationScale = Config.Voicevox.IntonationScale;
		querybody.VolumeScale = Config.Voicevox.VolumeScale;

		var filename = Path.GetTempFileName();
		using (var file = File.OpenWrite(filename))
		{
			using var audioResponse = await HttpClient.PostAsync($"synthesis?speaker=9", new StringContent(JsonSerializer.Serialize(querybody), Encoding.UTF8, "application/json"));
			await audioResponse.Content.CopyToAsync(file);
		}

		var ch = Bass.CreateStream(filename);
		Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, 1);
		Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) =>
		{
			Bass.StreamFree(ch);
			File.Delete(filename);
		});
		Bass.ChannelPlay(ch);
	}
}


