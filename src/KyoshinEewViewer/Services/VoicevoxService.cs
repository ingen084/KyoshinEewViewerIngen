using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Voicevox;
using ManagedBass;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace KyoshinEewViewer.Services;

public class VoicevoxService : ReactiveObject, IDisposable
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

	private readonly string _cacheDirectory;
	private Timer? _cacheCleanupTimer;
	private readonly ConcurrentDictionary<string, Task<string?>> _preparingTasks = new();


	public VoicevoxService(KyoshinEewViewerConfiguration config, ILogManager logManager, SoundPlayerService soundPlayerService)
	{
		SplatRegistrations.RegisterLazySingleton<VoicevoxService>();

		Config = config;
		Logger = logManager.GetLogger<VoicevoxService>();
		SoundPlayerService = soundPlayerService;
		
		_cacheDirectory = Path.Combine(Path.GetTempPath(), "KyoshinEewViewer", "VoicevoxCache");
		Directory.CreateDirectory(_cacheDirectory);
		
		// 起動直後(1分後)と1時間間隔で自動キャッシュクリーンアップを実行
		_cacheCleanupTimer = new Timer(s => CleanupVoicevoxCache(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1.1));
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

	private static string GenerateCacheKey(string text, int speakerId, float speedScale, float pitchScale, float intonationScale, float volumeScale, float pauseLengthScale)
	{
		var input = $"{text}_{speakerId}_{speedScale}_{pitchScale}_{intonationScale}_{volumeScale}_{pauseLengthScale}";
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private string GetCacheFilePath(string cacheKey)
		=> Path.Combine(_cacheDirectory, $"{cacheKey}.wav");

	public Task<string?> PrepareAudioAsync(string text)
	{
		if (!Config.Voicevox.Enabled)
			return Task.FromResult<string?>(null);

		var cacheKey = GenerateCacheKey(text, Config.Voicevox.SpeakerId, Config.Voicevox.SpeedScale, Config.Voicevox.PitchScale, Config.Voicevox.IntonationScale, Config.Voicevox.VolumeScale, Config.Voicevox.PauseLengthScale);
		var filename = GetCacheFilePath(cacheKey);

		// ファイル存在確認とアクセス時刻更新
		if (File.Exists(filename))
		{
			try
			{
				File.SetLastWriteTimeUtc(filename, DateTime.UtcNow);
			}
			catch (IOException) { }

			Logger.LogDebug($"音声キャッシュが見つかりました: {cacheKey}");
			return Task.FromResult<string?>(filename);
		}

		// 既に合成中のタスクがあればそれを返す、なければ新規に合成開始
		return _preparingTasks.GetOrAdd(cacheKey, _ => PrepareAudioCoreAsync(text, cacheKey, filename));
	}

	private async Task<string?> PrepareAudioCoreAsync(string text, string cacheKey, string filename)
	{
		try
		{
			var c = HttpUtility.ParseQueryString(string.Empty);
			c.Add("text", text);
			c.Add("speaker", Config.Voicevox.SpeakerId.ToString());

			using var response = await HttpClient.PostAsync(Config.Voicevox.Address + $"audio_query?{c}", null);
			if (!response.IsSuccessStatusCode)
			{
				Logger.LogWarning($"audio query の作成に失敗しています。 StatusCode:{response.StatusCode}");
				return null;
			}
			var querybody = await JsonSerializer.DeserializeAsync<Voicevox.Model.AudioQuery>(await response.Content.ReadAsStreamAsync());
			if (querybody is null)
			{
				Logger.LogWarning("audio query の作成に失敗しています。JSON のパースに失敗しました。");
				return null;
			}

			querybody.SpeedScale = Config.Voicevox.SpeedScale;
			querybody.PitchScale = Config.Voicevox.PitchScale;
			querybody.IntonationScale = Config.Voicevox.IntonationScale;
			querybody.VolumeScale = Config.Voicevox.VolumeScale;
			querybody.PauseLengthScale = Config.Voicevox.PauseLengthScale;

			using (var file = File.OpenWrite(filename))
			{
				using var audioResponse = await HttpClient.PostAsync(Config.Voicevox.Address + $"synthesis?speaker=" + Config.Voicevox.SpeakerId.ToString(), new StringContent(JsonSerializer.Serialize(querybody), Encoding.UTF8, "application/json"));
				if (!audioResponse.IsSuccessStatusCode)
				{
					Logger.LogWarning($"音声合成に失敗しています。 StatusCode:{audioResponse.StatusCode}");
					return null;
				}
				await audioResponse.Content.CopyToAsync(file);
			}

			Logger.LogDebug($"音声をキャッシュに保存しました: {cacheKey}");

			// 即時削除設定の場合、キャッシュから削除
			if (Config.Voicevox.ClearCacheImmediately)
			{
				try
				{
					File.Delete(filename);
					Logger.LogDebug($"即時削除設定により音声キャッシュを削除しました: {cacheKey}");
					return null;
				}
				catch (IOException ex)
				{
					Logger.LogWarning(ex, "音声キャッシュの即時削除に失敗しました");
				}
			}

			return filename;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "音声合成の準備に失敗しました");
			return null;
		}
		finally
		{
			_preparingTasks.TryRemove(cacheKey, out _);
		}
	}

	/// <summary>
	/// 複数テキストを合成しながら順次再生する（パイプライン処理）
	/// </summary>
	/// <param name="texts">再生するテキストのリスト</param>
	/// <param name="volume">音量</param>
	/// <param name="waitToEnd">trueの場合すべて再生完了まで待機、falseの場合最初の再生開始で戻る</param>
	public async Task PrepareAndPlaySequentiallyAsync(string[] texts, double volume, bool waitToEnd)
	{
		if (texts.Length == 0) return;

		async Task PlayAllAsync()
		{
			// 先読み合成タスクを管理するキュー
			var prepareQueue = new Queue<Task<string?>>();

			// 最初の2つを先行して合成開始（既に開始中ならそのタスクを使う）
			for (var i = 0; i < Math.Min(2, texts.Length); i++)
			{
				prepareQueue.Enqueue(PrepareAudioAsync(texts[i]));
			}

			var nextPrepareIndex = Math.Min(2, texts.Length);

			for (var i = 0; i < texts.Length; i++)
			{
				// 現在のセグメントの合成完了を待つ
				await prepareQueue.Dequeue();

				// 次のセグメントを先行して合成開始
				if (nextPrepareIndex < texts.Length)
				{
					prepareQueue.Enqueue(PrepareAudioAsync(texts[nextPrepareIndex]));
					nextPrepareIndex++;
				}

				// 再生
				await PlayAsync(texts[i], volume, waitToEnd: true);
			}
		}

		if (waitToEnd)
		{
			await PlayAllAsync();
		}
		else
		{
			// 最初のセグメント再生開始で戻り、残りはバックグラウンド
			_ = Task.Run(PlayAllAsync);
		}
	}

	public async Task PlayAsync(string text, double volume, bool waitToEnd)
	{
		if (!SoundPlayerService.IsAvailable)
			return;

		var cacheKey = GenerateCacheKey(text, Config.Voicevox.SpeakerId, Config.Voicevox.SpeedScale, Config.Voicevox.PitchScale, Config.Voicevox.IntonationScale, Config.Voicevox.VolumeScale, Config.Voicevox.PauseLengthScale);
		var cachedFilePath = GetCacheFilePath(cacheKey);

		if (!File.Exists(cachedFilePath))
		{
			Logger.LogDebug($"キャッシュされた音声が見つかりません: {cacheKey}");
			cachedFilePath = await PrepareAudioAsync(text);
			if (cachedFilePath is null)
				return;
		}

		try
		{
			var ch = Bass.CreateStream(cachedFilePath);
			if (ch == 0)
			{
				Logger.LogWarning($"CreateStream に失敗しています。 LastError:{Bass.LastError}");
				return;
			}
			Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, (float)volume);
			var mre = new ManualResetEventSlim(false);
			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) =>
			{
				Bass.StreamFree(ch);
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
			Logger.LogWarning(ex, "音声の再生に失敗しました");
		}
	}

	public void ClearCache()
	{
		try
		{
			if (Directory.Exists(_cacheDirectory))
			{
				foreach (var file in Directory.GetFiles(_cacheDirectory, "*.wav"))
				{
					File.Delete(file);
				}
			}
			Logger.LogDebug("音声キャッシュをクリアしました");
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "音声キャッシュのクリアに失敗しました");
		}
	}

	private void CleanupVoicevoxCache()
	{
		if (!Config.Voicevox.EnableAutoCacheCleanup || !Directory.Exists(_cacheDirectory))
			return;

		Logger.LogDebug("voicevox cache cleaning...");
		var s = DateTime.Now;
		var cutoffTime = DateTime.UtcNow.AddDays(-Config.Voicevox.CacheMaxDays);
		var deletedCount = 0;

		foreach (var file in Directory.GetFiles(_cacheDirectory, "*.wav"))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(file) <= cutoffTime)
				{
					File.Delete(file);
					deletedCount++;
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) 
			{
				Logger.LogWarning(ex, $"キャッシュファイルの削除に失敗: {file}");
			}
		}

		if (deletedCount > 0)
			Logger.LogDebug($"voicevox cache cleaning completed: {deletedCount} files deleted in {(DateTime.Now - s).TotalMilliseconds}ms");
	}

	public Task PlayTest()
		=> PlayAsync($"これは読み上げのテストです。現在の時刻は、{DateTime.Now:H時m分s秒}です", 1, false);

	public void Dispose()
	{
		_cacheCleanupTimer?.Dispose();
		HttpClient?.Dispose();
		GC.SuppressFinalize(this);
	}
}


