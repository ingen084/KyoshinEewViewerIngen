using KyoshinEewViewer.Core.Models;
using ManagedBass;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Services;

/// <summary>
/// 音声を再生するサービス
/// </summary>
public class SoundPlayerService
{
	internal KyoshinEewViewerConfiguration Config { get; }

	/// <summary>
	/// 利用可能かどうか
	/// </summary>
	public bool IsAvailable { get; }
#if DEBUG
	public Sound TestSound { get; }
#endif

	public SoundPlayerService(KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<SoundPlayerService>();

		Config = config;

		// とりあえず初期化を試みる
		try
		{
			IsAvailable = Bass.Init();
			if (IsAvailable)
			{
				Config.Audio.WhenAnyValue(x => x.GlobalVolume)
					.Subscribe(x => Bass.GlobalStreamVolume = (int)(Math.Clamp(x, 0, 1) * 10000));
				Bass.GlobalStreamVolume = (int)(Config.Audio.GlobalVolume * 10000);
			}
		}
		catch
		{
			IsAvailable = false;
		}
#if DEBUG
		TestSound = RegisterSound(
			new SoundCategory("Test", "テスト"),
			"TestPlay",
			"揺れ検出(震度1未満)",
			"{test}: 奇数秒|偶数秒\n{!test}: testを反転したもの",
			new()
			{
				{ "test", "奇数秒" },
				{ "!test", "偶数秒" },
			}
		);
#endif
	}
	~SoundPlayerService()
	{
		DisposeItems();
		if (IsAvailable)
			Bass.Free();
	}

	private Dictionary<SoundCategory, List<Sound>> Sounds { get; } = new();
	public IReadOnlyDictionary<SoundCategory, List<Sound>> RegisteredSounds => Sounds;

	public Sound RegisterSound(SoundCategory category, string name, string displayName, string? description = null, Dictionary<string, string>? exampleParameter = null)
	{
		if (Sounds.TryGetValue(category, out var sounds))
		{
			var sound = sounds.FirstOrDefault(s => s.Name == name);
			if (sound is not null)
				return sound;
			sound = new(this, category, name, displayName, description, exampleParameter);
			sounds.Add(sound);
			return sound;
		}
		var sound2 = new Sound(this, category, name, displayName, description, exampleParameter);
		Sounds.Add(category, new() { sound2 });
		return sound2;
	}

	public void DisposeItems()
	{
		foreach (var s in Sounds.SelectMany(s => s.Value).ToArray())
			s.Dispose();
		Sounds.Clear();
	}
}

public record struct SoundCategory(string Name, string DisplayName);
public class Sound : IDisposable
{
	internal Sound(SoundPlayerService service, SoundCategory parentCategory, string name, string displayName, string? description, IDictionary<string, string>? exampleParameter)
	{
		Service = service;
		ParentCategory = parentCategory;
		Name = name;
		DisplayName = displayName;
		Description = description;
		ExampleParameter = exampleParameter;
	}

	private SoundPlayerService Service { get; }
	public SoundCategory ParentCategory { get; }
	public string Name { get; }
	public string DisplayName { get; }
	public string? Description { get; }
	public IDictionary<string, string>? ExampleParameter { get; }

	// 設定を取得する 存在しなければ項目を作成する
	public KyoshinEewViewerConfiguration.SoundConfig Config
	{
		get {
			KyoshinEewViewerConfiguration.SoundConfig? config;
			if (!Service.Config.Sounds.TryGetValue(ParentCategory.Name, out var sounds))
			{
				config = new();
				Service.Config.Sounds[ParentCategory.Name] = new() { { Name, config } };
				return config;
			}
			if (sounds.TryGetValue(Name, out config))
				return config;

			return sounds[Name] = new();
		}
	}

	private string? LoadedFilePath { get; set; }
	private int? Channel { get; set; }

	private bool IsDisposed { get; set; }

	/// <summary>
	/// 音声を再生する
	/// </summary>
	public bool Play(Dictionary<string, string>? parameters = null)
	{
		var config = Config;

		if (!Service.IsAvailable || IsDisposed)
			return false;

		string GetFilePath()
		{
			if (string.IsNullOrWhiteSpace(config?.FilePath))
				return "";

			var useParams = parameters ?? ExampleParameter;
			if (useParams == null || useParams.Count == 0)
				return config.FilePath;

			// Dictionary の Key を {(key1|key2)} みたいなパターンに置換する
			var pattern = $"{{({string.Join('|', useParams.Select(kvp => Regex.Escape(kvp.Key)))})}}";
			// このパターンを使って置き換え
			return Regex.Replace(config.FilePath, pattern, m => useParams[m.Groups[1].Value]);
		}

		var filePath = GetFilePath();
		if (!config.Enabled || string.IsNullOrWhiteSpace(filePath))
			return false;

		// AllowMultiPlayが有効な場合クラス内部のキャッシュは使用しない
		// 再生ごとにファイルの読み込み･再生完了時に開放を行う
		if (config.AllowMultiPlay)
		{
			if (Channel is { } cachedChannel)
			{
				Bass.StreamFree(cachedChannel);
				LoadedFilePath = null;
			}
			var ch = Bass.CreateStream(filePath);
			if (ch == 0)
				return false;
			Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, Math.Clamp(config.Volume, 0, 1));
			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) => Bass.StreamFree(ch));

			return Bass.ChannelPlay(ch);
		}

		if (Channel is null or 0 || LoadedFilePath != filePath)
		{
			LoadedFilePath = null;
			if (Channel is { } cachedChannel)
				Bass.StreamFree(cachedChannel);
			Channel = Bass.CreateStream(filePath);
			if (Channel == 0)
				return false;
			LoadedFilePath = filePath;
		}

		if (Channel is { } c and not 0)
		{
			Bass.ChannelSetAttribute(c, ChannelAttribute.Volume, Math.Clamp(config.Volume, 0, 1));
			if (Bass.ChannelIsActive(c) != PlaybackState.Stopped)
				Bass.ChannelSetPosition(c, 0);
			return Bass.ChannelPlay(c);
		}
		return false;
	}

	public void Dispose()
	{
		if (Channel is { } i)
		{
			Bass.StreamFree(i);
			Channel = null;
			LoadedFilePath = null;
		}
		IsDisposed = true;
		GC.SuppressFinalize(this);
	}
}
