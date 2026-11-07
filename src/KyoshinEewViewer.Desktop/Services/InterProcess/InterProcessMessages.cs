using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Desktop.Services.InterProcess;

/// <summary>
/// KEVi のインスタンス間でやりとりするメッセージ
/// </summary>
public static class InterProcessMessages
{
	/// <summary>インスタンスの情報を問い合わせる</summary>
	public static InterProcessMessageType<InterProcessEmpty, InstanceInfo> GetInstanceInfo { get; }
		= new("GetInstanceInfo", InterProcessMessageJsonContext.Default.InterProcessEmpty, InterProcessMessageJsonContext.Default.InstanceInfo);

	/// <summary>メインウィンドウを表示させる</summary>
	public static InterProcessMessageType<InterProcessEmpty, InterProcessEmpty> ShowMainWindow { get; }
		= new("ShowMainWindow", InterProcessMessageJsonContext.Default.InterProcessEmpty, InterProcessMessageJsonContext.Default.InterProcessEmpty);
}

/// <summary>内容を持たない要求・応答</summary>
public sealed record InterProcessEmpty
{
	public static InterProcessEmpty Instance { get; } = new();
}

/// <summary>インスタンスの情報</summary>
/// <param name="ProcessId">プロセス ID</param>
/// <param name="StandaloneSeriesName">スタンドアロンで起動していれば表示している Series の名前</param>
/// <param name="Version">バージョン</param>
public sealed record InstanceInfo(int ProcessId, string? StandaloneSeriesName, string Version);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InterProcessEmpty))]
[JsonSerializable(typeof(InstanceInfo))]
internal partial class InterProcessMessageJsonContext : JsonSerializerContext;
