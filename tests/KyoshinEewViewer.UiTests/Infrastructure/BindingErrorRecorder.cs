using Avalonia.Logging;

namespace KyoshinEewViewer.UiTests.Infrastructure;

/// <summary>
/// 画面の表示中に発生したバインディングエラーを記録する<br/>
/// バインディングエラーは例外にならずログへ出るだけなので、表示崩れを検出するために使う
/// </summary>
internal sealed class BindingErrorRecorder : ILogSink, IDisposable
{
	private readonly ILogSink? _previous = Logger.Sink;
	private readonly List<string> _errors = [];

	public BindingErrorRecorder() => Logger.Sink = this;

	public string[] Errors
	{
		get {
			lock (_errors)
				return [.. _errors];
		}
	}

	public bool IsEnabled(LogEventLevel level, string area)
		=> area == LogArea.Binding && level >= LogEventLevel.Warning;

	public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
		=> Log(level, area, source, messageTemplate, []);

	public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
	{
		if (!IsEnabled(level, area))
			return;
		lock (_errors)
			_errors.Add($"{source}: {messageTemplate} [{string.Join(", ", propertyValues)}]");
	}

	public void Dispose() => Logger.Sink = _previous;
}
