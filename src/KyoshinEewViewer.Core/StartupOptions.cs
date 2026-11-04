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

	/// <summary>
	/// 結合テストビルド専用: メインウィンドウ表示後に画面操作のシナリオを実行し、結果を書き出して自動終了する
	/// </summary>
	[Option("ui-test", Required = false)]
	public bool UiTest { get; set; }

	/// <summary>
	/// 結合テストビルド専用: <see cref="UiTest"/> と併用し、前回の実行で保存した内容が残っていることを確認するシナリオを実行する
	/// </summary>
	[Option("ui-test-after-restart", Required = false)]
	public bool UiTestAfterRestart { get; set; }

	/// <summary>
	/// 既存のインスタンスに影響しないように、多重起動の検知とIPCサーバーを無効にする
	/// </summary>
	public bool IsIntegrationTestRun => SmokeTest || AutoUpdateTest || UiTest;
#endif
}
