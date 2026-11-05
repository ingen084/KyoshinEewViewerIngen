using System.Text;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.UiTests.Infrastructure;

/// <summary>
/// アプリケーションのログを記録し、テストの失敗時に原因を追えるようにする
/// </summary>
internal sealed class LogRecorder : ILoggerProvider
{
	private readonly Lock _gate = new();
	private readonly List<string> _entries = [];

	public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

	public bool Contains(LogLevel level, string text)
	{
		lock (_gate)
			return _entries.Any(e => e.StartsWith(level.ToString(), StringComparison.Ordinal) && e.Contains(text, StringComparison.Ordinal));
	}

	public override string ToString()
	{
		lock (_gate)
			return "ログ:\n" + string.Join("\n", _entries);
	}

	public void Dispose()
	{
	}

	/// <summary>
	/// テスト側で観測した状態の変化を記録する
	/// </summary>
	public void Add(string entry)
	{
		lock (_gate)
			_entries.Add(entry);
	}

	private sealed class Logger(LogRecorder owner, string categoryName) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;
			var entry = new StringBuilder($"{logLevel} [{categoryName.Split('.').Last()}] {formatter(state, exception)}");
			if (exception != null)
				entry.Append($" ({exception.GetType().Name}: {exception.Message})");
			owner.Add(entry.ToString());
		}
	}
}
