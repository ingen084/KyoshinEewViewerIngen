using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.Core;

/// <summary>
/// DI でロガーを受け取れない静的クラスなどから使う既定のロガー。
/// Splat の LogHost.Default の代替。
/// </summary>
public static class AppLog
{
	private static ILoggerFactory? _factory;

	/// <summary>
	/// 既定のロガー。<see cref="SetFactory"/> 呼び出し前は何も出力しない
	/// </summary>
	public static ILogger Default { get; private set; } = NullLogger.Instance;

	/// <summary>
	/// ロギング初期化時に <see cref="ILoggerFactory"/> を登録する
	/// </summary>
	public static void SetFactory(ILoggerFactory factory)
	{
		_factory = factory;
		Default = factory.CreateLogger("KyoshinEewViewer");
	}

	/// <summary>
	/// 型付きロガーを生成する
	/// </summary>
	public static ILogger<T> Create<T>()
		=> _factory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
}
