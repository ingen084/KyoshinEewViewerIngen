using System.Reflection;

// 自己更新の結合テストで「新バージョン」として配置されるダミー実行ファイル。
// 自己更新の完了後に本体の代わりに起動され、センチネルファイルでテストドライバに完了を通知する。
// センチネルの形式は KyoshinEewViewer.Core の IntegrationTestSentinel と揃えること。
// (トリミング有効のためリフレクションベースのJsonSerializerは使えず、手組みで出力する)
var version = Assembly.GetExecutingAssembly()
	.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
var payload = $$"""
{"Version":"{{version}}","Stage":"dummy-launched","Pid":{{Environment.ProcessId}},"WrittenAt":"{{DateTime.UtcNow:O}}"}
""";
File.WriteAllText(Path.Combine(exeDir, "kevi-startup-sentinel.json"), payload);
Console.WriteLine($"ダミー新バージョン {version} が起動しました");
