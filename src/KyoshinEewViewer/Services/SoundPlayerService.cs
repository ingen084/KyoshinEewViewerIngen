using KyoshinEewViewer.Core.Models;
using ManagedBass;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Services;

/// <summary>
/// 音声を再生するサービス
/// </summary>
public static class SoundPlayerService
{
	public class DestructorListener
	{
		~DestructorListener()
		{
			DisposeItems();
		}
	}
	public static DestructorListener Destructor { get; } = new();

	/// <summary>
	/// 利用可能かどうか
	/// </summary>
	public static bool IsAvailable { get; }
	public static Sound TestSound { get; }

	static SoundPlayerService()
	{
		// とりあえず初期化を試みる
		try
		{
			IsAvailable = Bass.Init();
		}
		catch
		{
			IsAvailable = false;
		}
		TestSound = RegisterSound(new SoundCategory("Test", "テスト"), "TestPlay", "テスト再生用音声");
	}

	private static Dictionary<SoundCategory, List<Sound>> Sounds { get; } = new();

	public static Sound RegisterSound(SoundCategory category, string name, string displayName)
	{
		// 設定を取得する 存在しなければ項目を作成する
		if (!ConfigurationService.Current.Sounds.TryGetValue(category.Name, out var soundConfigs))
			ConfigurationService.Current.Sounds[category.Name] = new() { { name, new() } };
		else if (!soundConfigs.TryGetValue(name, out _))
			soundConfigs[name] = new();

		if (Sounds.TryGetValue(category, out var sounds))
		{
			var sound = sounds.FirstOrDefault(s => s.Name == name);
			if (sound is not null)
				return sound;
			sound = new(category, name, displayName);
			sounds.Add(sound);
			return sound;
		}
		var sound2 = new Sound(category, name, displayName);
		Sounds.Add(category, new() { sound2 });
		return sound2;
	}

	public static void DisposeItems()
	{
		foreach (var s in Sounds.SelectMany(s => s.Value).ToArray())
			s.Dispose();
		Sounds.Clear();
	}

	public record struct SoundCategory(string Name, string DisplayName);
	public class Sound : IDisposable
	{
		internal Sound(SoundCategory parentCategory, string name, string displayName)
		{
			ParentCategory = parentCategory;
			Name = name;
			DisplayName = displayName;
		}

		public SoundCategory ParentCategory { get; }
		public string Name { get; }
		public string DisplayName { get; }

		private string? LoadedFilePath { get; set; }
		private int? Channel { get; set; }

		private bool IsDisposed { get; set; }

		public void Play()
		{
			if (!IsAvailable || IsDisposed)
				return;

			// 設定を取得する 存在しなければ項目を作成する
			KyoshinEewViewerConfiguration.SoundConfig? config;
			if (!ConfigurationService.Current.Sounds.TryGetValue(ParentCategory.Name, out var sounds))
			{
				config = new();
				ConfigurationService.Current.Sounds[ParentCategory.Name] = sounds = new() { { Name, config } };
			}
			else if (!sounds.TryGetValue(Name, out config))
				sounds[Name] = config = new();

			if (!config.Enabled || string.IsNullOrWhiteSpace(config.FilePath))
				return;

			// AllowMultiPlayが有効な場合クラス内部のキャッシュは使用しない
			// 再生ごとにファイルの読み込み･再生完了時に開放を行う
			if (config.AllowMultiPlay)
			{
				if (Channel is int cachedChannel)
				{
					Bass.StreamFree(cachedChannel);
					LoadedFilePath = null;
				}
				var ch = Bass.CreateStream(config.FilePath);
				if (ch == 0)
					return;
				Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) => Bass.StreamFree(ch));
				Bass.ChannelPlay(ch);
				return;
			}

			if (Channel is null or 0 || LoadedFilePath != config.FilePath)
			{
				LoadedFilePath = null;
				if (Channel is int cachedChannel)
					Bass.StreamFree(cachedChannel);
				Channel = Bass.CreateStream(config.FilePath);
				if (Channel == 0)
					return;
				LoadedFilePath = config.FilePath;
			}

			if (Channel is int c and not 0)
			{
				if (Bass.ChannelIsActive(c) != PlaybackState.Stopped)
					Bass.ChannelSetPosition(c, 0);
				Bass.ChannelPlay(c);
			}
		}

		public void Dispose()
		{
			if (Channel is int i)
			{
				Bass.StreamFree(i);
				Channel = null;
				LoadedFilePath = null;
			}
			IsDisposed = true;
			GC.SuppressFinalize(this);
		}

	}
}
