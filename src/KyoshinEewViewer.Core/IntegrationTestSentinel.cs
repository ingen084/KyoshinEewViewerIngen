#if INTEGRATION_TEST
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core;

/// <summary>
/// 結合テストビルド専用: テストドライバが起動完了を検出するためのセンチネルファイルを書き出す
/// </summary>
public static class IntegrationTestSentinel
{
	public const string FileName = "kevi-startup-sentinel.json";

	/// <summary>
	/// 起動時イベントで動いたワークフローがすべて終わったら完了する
	/// </summary>
	public static TaskCompletionSource StartupWorkflowsCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	/// 実行ファイルと同じディレクトリにセンチネルファイルを書き出す
	/// </summary>
	/// <param name="stage">どの段階まで到達したかを示す文字列</param>
	public static void Write(string stage)
	{
		var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
		var payload = JsonSerializer.Serialize(new SentinelPayload(Utils.InternalVersion, stage, Environment.ProcessId, DateTime.UtcNow));
		File.WriteAllText(Path.Combine(exeDir, FileName), payload);
	}

	private record SentinelPayload(string Version, string Stage, int Pid, DateTime WrittenAt);
}
#endif
