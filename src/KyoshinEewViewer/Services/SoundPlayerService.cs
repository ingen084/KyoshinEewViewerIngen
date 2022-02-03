using KyoshinEewViewer.Core.Models;
using ManagedBass;
using ReactiveUI;
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
			if (IsAvailable)
				Bass.Free();
		}
	}
	public static DestructorListener Destructor { get; } = new();

	/// <summary>
	/// 利用可能かどうか
	/// </summary>
	public static bool IsAvailable { get; }
#if DEBUG
	public static Sound TestSound { get; }
#endif

	static SoundPlayerService()
	{
		// とりあえず初期化を試みる
		try
		{
			IsAvailable = Bass.Init();
			if (IsAvailable)
			{
				ConfigurationService.Current.Audio.WhenAnyValue(x => x.GlobalVolume)
					.Subscribe(x => Bass.GlobalStreamVolume = (int)(Math.Clamp(x, 0, 1) * 10000));
				Bass.GlobalStreamVolume = (int)(ConfigurationService.Current.Audio.GlobalVolume * 10000);
			}
		}
		catch
		{
			IsAvailable = false;
		}
#if DEBUG
		TestSound = RegisterSound(new SoundCategory("Test", "テスト"), "TestPlay", "テスト再生用音声");
#endif
	}

	private static Dictionary<SoundCategory, List<Sound>> Sounds { get; } = new();
	public static IReadOnlyDictionary<SoundCategory, List<Sound>> RegisteredSounds => Sounds;

	public static Sound RegisterSound(SoundCategory category, string name, string displayName)
	{
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

		// 設定を取得する 存在しなければ項目を作成する
		public KyoshinEewViewerConfiguration.SoundConfig Config
		{
			get {
				KyoshinEewViewerConfiguration.SoundConfig? config;
				if (!ConfigurationService.Current.Sounds.TryGetValue(ParentCategory.Name, out var sounds))
				{
					config = new();
					ConfigurationService.Current.Sounds[ParentCategory.Name] = new() { { Name, config } };
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
		public bool Play()
		{
			if (!IsAvailable || IsDisposed)
				return false;

			var config = Config;
			if (!config.Enabled || string.IsNullOrWhiteSpace(config.FilePath))
				return false;

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
					return false;
				Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, Math.Clamp(config.Volume, 0, 1));
				Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) => Bass.StreamFree(ch));

				return Bass.ChannelPlay(ch);
			}

			if (Channel is null or 0 || LoadedFilePath != config.FilePath)
			{
				LoadedFilePath = null;
				if (Channel is int cachedChannel)
					Bass.StreamFree(cachedChannel);
				Channel = Bass.CreateStream(config.FilePath);
				if (Channel == 0)
					return false;
				LoadedFilePath = config.FilePath;
			}

			if (Channel is int c and not 0)
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
