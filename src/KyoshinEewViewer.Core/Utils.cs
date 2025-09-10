using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KyoshinEewViewer.Core;

public static class Utils
{
	public static string IconFontName => "avares://KyoshinEewViewer.Core/Assets/Fonts/FontAwesome6Free-Solid-900.otf#Font Awesome 6 Free";
	public static string? OverrideVersion { get; set; }
	
	/// <summary>
	/// 表示用バージョン文字列（DEBUG, EXP-123などもそのまま表示）
	/// </summary>
	public static string Version
	{
		get {
			if (OverrideVersion != null)
				return OverrideVersion;
			
			// AssemblyInformationalVersionを優先使用（suffix含む）
			var informationalVersion = Assembly.GetExecutingAssembly()
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			
			if (!string.IsNullOrEmpty(informationalVersion) && informationalVersion.Contains('-'))
				return informationalVersion.Replace("-", "\n");
			
			// フォールバック: 既存の処理
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			if (ver == null)
				return "?";
			// 0.1.1.X は手元ビルドかリリース外のビルド
			if (ver is { Major: 0, Minor: 1, Build: 1 })
			{
				if (ver.Revision != 0)
					return "EXP-" + ver.Revision;
				return "DEBUG";
			}
			return ver.ToString();
		}
	}
	
	/// <summary>
	/// 内部処理用バージョン文字列（比較可能な形式で返す）
	/// </summary>
	public static string InternalVersion
	{
		get {
			if (OverrideVersion != null)
				return OverrideVersion;
			
			// AssemblyInformationalVersionを優先使用（suffix含む）
			var informationalVersion = Assembly.GetExecutingAssembly()
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			
			if (!string.IsNullOrEmpty(informationalVersion))
				return informationalVersion;
			
			// フォールバック: 特殊バージョンを比較可能な形式で処理
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			if (ver == null)
				return "0.0.0.0-unknown";
			// 0.1.1.X は手元ビルドかリリース外のビルド
			if (ver is { Major: 0, Minor: 1, Build: 1 })
			{
				if (ver.Revision != 0)
					return $"0.1.1.{ver.Revision}-exp";
				return "0.0.0.0-debug";
			}
			return ver.ToString();
		}
	}

	/// <summary>
	/// 全角文字を半角に変換する
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string ConvertToShortWidthString(string str)
		=> str switch
		{
			"ただちに津波来襲と予測" => "ただちに来襲",
			"津波到達中と推測" => "津波到達中",
			"第１波の到達を確認" => "第1波到達",
			_ => new StringBuilder(str)
				.Replace('０', '0')
				.Replace('１', '1')
				.Replace('２', '2')
				.Replace('３', '3')
				.Replace('４', '4')
				.Replace('５', '5')
				.Replace('６', '6')
				.Replace('７', '7')
				.Replace('８', '8')
				.Replace('９', '9')
				.Replace('ｍ', 'm')
				.Replace('．', '.')
				.ToString()
	};

	public static bool IsAppRunning => Process.GetProcessesByName("KyoshinEewViewer").Count(p => p.Responding) > 1;

	/// <summary>
	/// バージョン文字列をパースしてベースバージョンとsuffixに分離する
	/// </summary>
	/// <param name="versionString">1.0.0.123-alpha形式のバージョン文字列</param>
	/// <returns>(baseVersion, suffix)</returns>
	public static (Version version, string? suffix) ParseVersionString(string versionString)
	{
		var parts = versionString.Split('-', 2);
		var version = System.Version.TryParse(parts[0], out var v) ? v : new Version();
		var suffix = parts.Length > 1 ? parts[1] : null;
		return (version, suffix);
	}

	/// <summary>
	/// suffixの優先度を取得する
	/// </summary>
	/// <param name="suffix">suffix文字列</param>
	/// <returns>優先度（高いほど新しい）</returns>
	public static int GetSuffixPriority(string? suffix)
	{
		return suffix switch
		{
			null => 1000, // stable
			"rc" => 100,
			"beta" => 10,
			"alpha" => 1,
			"exp" => -10, // 実験版（開発版より低い優先度）
			"debug" => -100, // デバッグ版
			"unknown" => -1000, // 不明
			_ => 0
		};
	}


	/// <summary>
	/// suffixが新しいかどうかを判定する
	/// </summary>
	/// <param name="currentSuffix">現在のsuffix</param>
	/// <param name="releaseSuffix">リリースのsuffix</param>
	/// <returns>新しい場合true</returns>
	public static bool IsNewerSuffix(string? currentSuffix, string? releaseSuffix)
	 	=> GetSuffixPriority(releaseSuffix) > GetSuffixPriority(currentSuffix);
}
