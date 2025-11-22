using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Services;

/// <summary>
/// ログエントリ
/// </summary>
public class LogEntry
{
	public DateTime Timestamp { get; set; }
	public LogLevel LogLevel { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public Exception? Exception { get; set; }
}

/// <summary>
/// ログ追加イベント
/// </summary>
public class LogEntryAdded
{
	public required LogEntry Entry { get; init; }
}

/// <summary>
/// メモリ内ログプロバイダー
/// </summary>
public class InMemoryLoggerProvider : ILoggerProvider
{
	private readonly ConcurrentQueue<LogEntry> _logs = new();
	private readonly int _maxLogCount;

	public InMemoryLoggerProvider(int maxLogCount = 1000)
	{
		_maxLogCount = maxLogCount;
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new InMemoryLogger(this, categoryName);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// ログを追加
	/// </summary>
	internal void AddLog(LogEntry entry)
	{
		_logs.Enqueue(entry);

		// 最大数を超えたら古いログを削除
		while (_logs.Count > _maxLogCount)
			_logs.TryDequeue(out _);

		// ログ追加を通知
		MessageBus.Current.SendMessage(new LogEntryAdded { Entry = entry });
	}

	/// <summary>
	/// すべてのログを取得
	/// </summary>
	public IEnumerable<LogEntry> GetLogs()
	{
		return _logs.ToArray();
	}

	/// <summary>
	/// ログをクリア
	/// </summary>
	public void Clear()
	{
		_logs.Clear();
	}

	private class InMemoryLogger : ILogger
	{
		private readonly InMemoryLoggerProvider _provider;
		private readonly string _categoryName;

		public InMemoryLogger(InMemoryLoggerProvider provider, string categoryName)
		{
			_provider = provider;
			_categoryName = categoryName;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel != LogLevel.None;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;

			var message = formatter(state, exception);

			_provider.AddLog(new LogEntry
			{
				Timestamp = DateTime.Now,
				LogLevel = logLevel,
				CategoryName = _categoryName,
				Message = message,
				Exception = exception
			});
		}
	}
}
