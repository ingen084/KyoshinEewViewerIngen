using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using ManagedBass;
using R3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services;

/// <summary>
/// 音声を再生するサービス
/// </summary>
public partial class SoundPlayerService : ObservableObject
{
	internal KyoshinEewViewerConfiguration Config { get; }

	/// <summary>
	/// 利用可能かどうか
	/// </summary>
	[ObservableProperty]
	public partial bool IsAvailable { get; private set; }

	/// <summary>
	/// 使用中の再生デバイス (null の場合は既定のデバイス)
	/// </summary>
	[ObservableProperty]
	public partial AudioDevice? CurrentDevice { get; private set; }

	/// <summary>
	/// 保存した再生デバイスを使用できず、既定のデバイスで代用しているか
	/// </summary>
	[ObservableProperty]
	public partial bool IsFallbackToDefault { get; private set; }

	/// <summary>
	/// 再生デバイスの切り替えと、ストリームの作成を排他するためのロック
	/// </summary>
	/// <remarks>切り替え中に作成したストリームは解放されたデバイスに属してしまうため、切り替えが終わるまで待たせる</remarks>
	internal Lock BassLock { get; } = new();
#if DEBUG
	public Sound TestSound { get; }
#endif
	internal ILogger Logger { get; }

	public SoundPlayerService(KyoshinEewViewerConfiguration config, ILogger<SoundPlayerService> logger)
	{
		Config = config;
		Logger = logger;

		// とりあえず初期化を試みる
		try
		{
			if (Config.Audio.DeviceName is not { } deviceName)
				TryInit(null, null);
			else if (!TryInit(deviceName, Config.Audio.DeviceDriver))
			{
				Logger.LogWarning("保存した再生デバイスを使用できないため、既定のデバイスを使用します: {DeviceName} ({DeviceDriver})", deviceName, Config.Audio.DeviceDriver);
				IsFallbackToDefault = TryInit(null, null);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Bass の初期化に失敗しました");
			IsAvailable = false;
		}

		Config.Audio.ObservePropertyChanged(x => x.GlobalVolume)
			.Subscribe(_ => UpdateVolume());
		Config.Audio.ObservePropertyChanged(x => x.IsMuted)
			.Subscribe(_ => UpdateVolume());
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

	private Dictionary<SoundCategory, List<Sound>> Sounds { get; } = [];
	public IReadOnlyDictionary<SoundCategory, List<Sound>> RegisteredSounds => Sounds;

	/// <summary>
	/// 利用できる再生デバイスの一覧を取得する
	/// </summary>
	public IReadOnlyList<AudioDevice> GetOutputDevices()
	{
		var devices = new List<AudioDevice>();
		try
		{
			// 0 は「No sound」のため 1 から列挙する
			for (var i = 1; Bass.GetDeviceInfo(i, out var info); i++)
			{
				Logger.LogDebug("再生デバイス {Index}: {Name} ({Driver}) Enabled:{IsEnabled} Default:{IsDefault}", i, info.Name, info.Driver, info.IsEnabled, info.IsDefault);
				// BASS が用意する「Default」(Windows はドライバー名なし、Linux は ALSA の default) は既定のデバイスと同じものなので除外する
				if (!info.IsEnabled || info.Name is null || string.IsNullOrEmpty(info.Driver) || info.Driver == "default")
					continue;
				devices.Add(new(i, info.Name, info.Driver));
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "再生デバイスの一覧を取得できませんでした");
		}
		return devices;
	}

	/// <summary>
	/// 名前とドライバー名から再生デバイスを探す
	/// </summary>
	/// <remarks>
	/// 両方が一致するものを優先し、なければ名前だけが一致するものを返す。
	/// Linux ではドライバー名に ALSA のカード番号が含まれることがあり、起動ごとに変わりうるため
	/// </remarks>
	internal static AudioDevice? FindDevice(IEnumerable<AudioDevice> devices, string name, string? driver)
	{
		AudioDevice? nameMatched = null;
		foreach (var device in devices)
		{
			if (device.Name != name)
				continue;
			if (device.Driver == driver)
				return device;
			nameMatched ??= device;
		}
		return nameMatched;
	}

	/// <summary>
	/// 再生デバイスを切り替え、設定に保存する
	/// </summary>
	/// <param name="device">切り替え先のデバイス (null の場合は既定のデバイス)</param>
	/// <returns>切り替えに成功したか (失敗した場合は元のデバイスに戻す)</returns>
	public bool ChangeDevice(AudioDevice? device)
	{
		lock (BassLock)
		{
			var previous = CurrentDevice;
			var wasAvailable = IsAvailable;

			// BASS はスレッドごとに使うデバイスを選ぶ仕組みのため、複数のデバイスを初期化すると
			// デバイスを指定していないスレッドは番号の小さいデバイスで再生してしまう。
			// 常に1台だけが初期化された状態にするため、先に解放してから初期化する
			FreeDevice();
			if (TryInit(device?.Name, device?.Driver))
			{
				Config.Audio.DeviceName = device?.Name;
				Config.Audio.DeviceDriver = device?.Driver;
				IsFallbackToDefault = false;
				return true;
			}

			if (wasAvailable && !TryInit(previous?.Name, previous?.Driver))
			{
				Logger.LogWarning("元の再生デバイスに戻せませんでした: {DeviceName} ({DeviceDriver})", previous?.Name ?? "既定のデバイス", previous?.Driver);
				IsFallbackToDefault = false;
			}
			return false;
		}
	}

	/// <summary>
	/// 再生デバイスを初期化する
	/// </summary>
	/// <param name="name">デバイスの名前 (null の場合は既定のデバイス)</param>
	/// <param name="driver">デバイスのドライバー名</param>
	private bool TryInit(string? name, string? driver)
	{
		AudioDevice? device = null;
		if (name is not null)
		{
			// 番号は起動中にも変わることがあるため、初期化の直前に探し直す
			device = FindDevice(GetOutputDevices(), name, driver);
			if (device is null)
			{
				Logger.LogWarning("再生デバイスが見つかりません: {DeviceName} ({DeviceDriver})", name, driver);
				return false;
			}
		}

		if (!Bass.Init(device?.Index ?? -1))
		{
			Logger.LogWarning("Bass の初期化に失敗しました。 LastError:{LastError} Device:{DeviceName} ({DeviceDriver})", Bass.LastError, name ?? "既定のデバイス", driver);
			return false;
		}

		Logger.LogInformation("再生デバイスを初期化しました: {DeviceName} ({DeviceDriver})", device?.Name ?? "既定のデバイス", device?.Driver);
		CurrentDevice = device;
		IsAvailable = true;
		UpdateVolume();
		return true;
	}

	private void FreeDevice()
	{
		if (!IsAvailable)
			return;

		// デバイス上のストリームはすべて解放されるため、キャッシュしているチャンネルも破棄する
		foreach (var sound in Sounds.SelectMany(s => s.Value))
			sound.ResetChannel();
		Bass.Free();
		IsAvailable = false;
	}

	private void UpdateVolume()
	{
		if (!IsAvailable)
			return;
		var volume = Config.Audio.IsMuted ? 0 : Config.Audio.GlobalVolume;
		Bass.GlobalStreamVolume = (int)(Math.Clamp(volume, 0, 1) * 10000);
	}

	public async Task<bool> PlayAsync(string path, double volume, bool waitToEnd)
	{
		var mre = new ManualResetEventSlim(false);
		lock (BassLock)
		{
			if (!IsAvailable)
			{
				Logger.LogWarning("音声を再生できません。Bass が利用できません。 Path:{Path}", path);
				return false;
			}

			var ch = Bass.CreateStream(path);
			if (ch == 0)
			{
				Logger.LogWarning("音声ストリームの作成に失敗しました。 LastError:{LastError} Exists:{Exists} Path:{Path}", Bass.LastError, File.Exists(path), path);
				return false;
			}
			Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, Math.Clamp(volume, 0, 1));

			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) =>
			{
				Bass.StreamFree(ch);
				mre.Set();
			});
			// 再生デバイスの切り替えで解放された場合は End が来ないため、解放時にも待機を終える
			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.Free, 0, (handle, channel, data, user) => mre.Set());
			if (!Bass.ChannelPlay(ch))
			{
				Logger.LogWarning("音声の再生開始に失敗しました。 LastError:{LastError} Path:{Path}", Bass.LastError, path);
				Bass.StreamFree(ch);
				return false;
			}
		}

		if (waitToEnd)
			await Task.Run(mre.Wait);
		return true;
	}

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
		Sounds.Add(category, [sound2]);
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

/// <summary>
/// 音声の再生デバイス
/// </summary>
/// <param name="Index">BASS のデバイス番号 (起動中にも変わることがあるため保存しない)</param>
/// <param name="Name">デバイスの名前</param>
/// <param name="Driver">ドライバー名 (Windows はエンドポイントID、macOS は UID、Linux は ALSA のデバイス名)</param>
public record class AudioDevice(int Index, string Name, string? Driver);
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
	public bool Play() => Play(null);

	/// <summary>
	/// 音声を再生する (設定画面の Command バインド用)
	/// </summary>
	/// <remarks>
	/// オーバーロードを持つ <see cref="Play()"/> を XAML から直接バインドすると
	/// コンパイル済みバインディングが 1引数版を選択してしまうため、別名のメソッドを用意している
	/// </remarks>
	public bool PlayFromGui() => Play(null);

	/// <summary>
	/// 音声を再生する
	/// </summary>
	/// <param name="parameters">ファイルパスの置換に使用するパラメータ</param>
	public bool Play(Dictionary<string, string>? parameters)
	{
		var config = Config;

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

		lock (Service.BassLock)
			return PlayCore(config, filePath);
	}

	private bool PlayCore(KyoshinEewViewerConfiguration.SoundConfig config, string filePath)
	{
		if (!Service.IsAvailable)
		{
			Service.Logger.LogWarning("音声を再生できません。Bass が利用できません。 Sound:{Name}/{Name2} Path:{FilePath}", ParentCategory.Name, Name, filePath);
			return false;
		}

		if (IsDisposed)
		{
			Service.Logger.LogWarning("破棄済みの音声を再生しようとしました。 Sound:{Name}/{Name2} Path:{FilePath}", ParentCategory.Name, Name, filePath);
			return false;
		}

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
			{
				Service.Logger.LogWarning("音声ストリームの作成に失敗しました。 LastError:{LastError} Exists:{Exists} Sound:{Name}/{Name2} Path:{FilePath}", Bass.LastError, File.Exists(filePath), ParentCategory.Name, Name, filePath);
				return false;
			}
			Bass.ChannelSetAttribute(ch, ChannelAttribute.Volume, Math.Clamp(config.Volume, 0, 1));
			Bass.ChannelSetSync(ch, SyncFlags.Onetime | SyncFlags.End, 0, (handle, channel, data, user) => Bass.StreamFree(ch));

			if (Bass.ChannelPlay(ch))
				return true;
			Service.Logger.LogWarning("音声の再生開始に失敗しました。 LastError:{LastError} Sound:{Name}/{Name2} Path:{FilePath}", Bass.LastError, ParentCategory.Name, Name, filePath);
			Bass.StreamFree(ch);
			return false;
		}

		if (Channel is null or 0 || LoadedFilePath != filePath)
		{
			LoadedFilePath = null;
			if (Channel is { } cachedChannel)
				Bass.StreamFree(cachedChannel);
			Channel = Bass.CreateStream(filePath);
			if (Channel == 0)
			{
				Service.Logger.LogWarning("音声ストリームの作成に失敗しました。 LastError:{LastError} Exists:{Exists} Sound:{Name}/{Name2} Path:{FilePath}", Bass.LastError, File.Exists(filePath), ParentCategory.Name, Name, filePath);
				return false;
			}
			LoadedFilePath = filePath;
		}

		if (Channel is { } c and not 0)
		{
			Bass.ChannelSetAttribute(c, ChannelAttribute.Volume, Math.Clamp(config.Volume, 0, 1));
			if (Bass.ChannelIsActive(c) != PlaybackState.Stopped)
				Bass.ChannelSetPosition(c, 0);
			if (Bass.ChannelPlay(c))
				return true;
			Service.Logger.LogWarning("音声の再生開始に失敗しました。 LastError:{LastError} Sound:{Name}/{Name2} Path:{FilePath}", Bass.LastError, ParentCategory.Name, Name, filePath);
			return false;
		}
		return false;
	}

	/// <summary>
	/// キャッシュしているチャンネルを破棄する (再生デバイスを解放するとチャンネルも解放されるため)
	/// </summary>
	internal void ResetChannel()
	{
		Channel = null;
		LoadedFilePath = null;
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
