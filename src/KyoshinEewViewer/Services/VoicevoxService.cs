using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Voicevox;
using ManagedBass;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace KyoshinEewViewer.Services;

public class VoicevoxService : ReactiveObject
{
	private KyoshinEewViewerConfiguration Config { get; }
	private HttpClient HttpClient { get; } = new();
	private SoundPlayerService SoundPlayerService { get; }
	private ILogger Logger { get; }

	private Speaker[] _speakers = [];
	public Speaker[] Speakers
	{
		get => _speakers;
		private set => this.RaiseAndSetIfChanged(ref _speakers, value);
	}

	private bool _speakersLoading = false;
	public bool SpeakersLoading
	{
		get => _speakersLoading;
		private set => this.RaiseAndSetIfChanged(ref _speakersLoading, value);
	}


	public VoicevoxService(KyoshinEewViewerConfiguration config, ILogManager logManager, SoundPlayerService soundPlayerService)
	{
		SplatRegistrations.RegisterLazySingleton<VoicevoxService>();

		Config = config;
		Logger = logManager.GetLogger<VoicevoxService>();
		SoundPlayerService = soundPlayerService;
	}

	public async Task GetSpeakers()
	{
		try
		{
			SpeakersLoading = true;

			using var response = await HttpClient.GetAsync(Config.Voicevox.Address + "speakers");
			var body = await response.Content.ReadAsStringAsync();
			Speakers = JsonSerializer.Deserialize<Voicevox.Model.Speaker[]>(body)?.Select<Voicevox.Model.Speaker, Speaker>(s
				=> s.Styles?.Length == 1 ?
					new SingleStyleSpeaker(s.Name ?? "不明", s.Styles[0].Id) :
					new MultiStyleSpeaker(s.Name ?? "不明", s.Styles?.Select(st => new SingleStyleSpeaker($"{s.Name}({st.Name})", st.Id)).ToArray() ?? [])
			).ToArray() ?? [];
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "話者の取得に失敗しました");
			Speakers = [];
		}
		finally
		{
			SpeakersLoading = false;
		}
	}

	public async Task PlayAsync(string text, bool waitToEnd)
	{
		if (!Config.Voicevox.Enabled || !SoundPlayerService.IsAvailable)
			return;
		try
		{
			var c = HttpUtility.ParseQueryString(string.Empty);
			c.Add("text", text);
			c.Add("speaker", Config.Voicevox.SpeakerId.ToString());

			using var response = await HttpClient.PostAsync(Config.Voicevox.Address + $"audio_query?{c}", null);
			if (!response.IsSuccessStatusCode)
			{
				Logger.LogWarning($"audio query の作成に失敗しています。 StatusCode:{response.StatusCode}");
				return;
			}
			var querybody = await JsonSerializer.DeserializeAsync<Voicevox.Model.AudioQuery>(await response.Content.ReadAsStreamAsync());
			if (querybody is null)
			{
				Logger.LogWarning("audio query の作成に失敗しています。JSON のパースに失敗しました。");
				return;
			}

			querybody.SpeedScale = Config.Voicevox.SpeedScale;
			querybody.PitchScale = Config.Voicevox.PitchScale;
			querybody.IntonationScale = Config.Voicevox.IntonationScale;
			querybody.VolumeScale = Config.Voicevox.VolumeScale;

			var filename = Path.GetTempFileName();
			using (var file = File.OpenWrite(filename))
			{
				using var audioResponse = await HttpClient.PostAsync(Config.Voicevox.Address + $"synthesis?speaker=" + Config.Voicevox.SpeakerId.ToString(), new StringContent(JsonSerializer.Serialize(querybody), Encoding.UTF8, "application/json"));
				if (!audioResponse.IsSuccessStatusCode)
				{
					Logger.LogWarning($"音声合成に失敗しています。 StatusCode:{response.StatusCode}");
					return;
				}
				await audioResponse.Content.CopyToAsync(file);
			}

			var ch = Bass.CreateStream(filename);
			if (ch == 0)
			{
				Logger.LogWarning($"CreateStream に失敗しています。 LastError:{Bass.LastError}");
				return;
			}
			Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, 1);
			var mre = new ManualResetEventSlim(false);
			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) =>
			{
				Bass.StreamFree(ch);
				File.Delete(filename);
				mre.Set();

			});
			if (Bass.ChannelPlay(ch))
			{
				if (waitToEnd)
					await Task.Run(mre.Wait);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "読み上げに失敗しました");
		}
	}

	public Task PlayTest()
		=> PlayAsync($"これは読み上げのテストです。現在の時刻は、{DateTime.Now:H時m分s秒}です", false);
}


