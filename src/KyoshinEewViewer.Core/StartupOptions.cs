using CommandLine;

namespace KyoshinEewViewer.Core;

public class StartupOptions
{
	public static StartupOptions? Current { get; set; }

	[Option('c', "CurrentDirectory", Required = false)]
	public string? CurrentDirectory { get; set; }

	[Option('s', "Standalone", Required = false)]
	public string? StandaloneSeriesName { get; set; }

	[Option('l', "console-log", Required = false)]
	public bool ConsoleLog { get; set; }

	[Option('d', "debug", Required = false)]
	public bool DebugLog { get; set; }

	[Option('n', "no-logo", Required = false)]
	public bool NoSplash { get; set; }

#if INTEGRATION_TEST
	/// <summary>
	/// 結合テストビルド専用: 更新チェックで更新を検出したら自動で自己更新を開始する
	/// </summary>
	[Option("auto-update-test", Required = false)]
	public bool AutoUpdateTest { get; set; }

	/// <summary>
	/// 結合テストビルド専用: メインウィンドウ表示後にセンチネルファイルを書き出して自動終了する
	/// </summary>
	[Option("smoke-test", Required = false)]
	public bool SmokeTest { get; set; }
#endif
}
