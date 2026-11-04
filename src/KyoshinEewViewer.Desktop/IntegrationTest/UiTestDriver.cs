#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using KyoshinEewViewer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 結合テストビルド専用: トリミング済みの実バイナリの中で画面を操作し、結果をファイルへ書き出す
/// </summary>
/// <remarks>
/// ここから静的に参照したメンバーはトリミングされなくなる。
/// 名前で読まれるメンバーを参照すると消失を検出できなくなるので、確認は表示されたテキストと保存されたファイルで行う
/// </remarks>
internal static class UiTestDriver
{
	private const string ResultFileName = "kevi-ui-test-result.txt";
	private const string WorkflowsFileName = "workflows.json";

	private static readonly string ResultPath = Path.GetFullPath(ResultFileName);
	private static readonly Lock ResultLock = new();
	private static BindingErrorSink? _bindingErrors;

	/// <summary>
	/// バインディングエラーの記録を開始する。ウィンドウの表示より前に呼ぶ
	/// </summary>
	public static void AttachBindingErrorSink()
		=> Logger.Sink = _bindingErrors = new BindingErrorSink(Logger.Sink);

	/// <summary>
	/// シナリオを実行し、結果を書き出してアプリを終了する
	/// </summary>
	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop)
	{
		// モーダルダイアログが閉じないとシナリオは先へ進めないので、別のスレッドで時間切れを監視する
		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromMinutes(5));
			WriteResult("RESULT: FAILED (時間切れ)");
			Environment.Exit(2);
		});

		var passed = false;
		try
		{
			// プロファイルにファイルがないと、ユーザーの設定ディレクトリのワークフローを読み書きしてしまう
			if (!File.Exists(WorkflowsFileName))
				throw new InvalidOperationException($"プロファイルに {WorkflowsFileName} がないため実行できません: {Environment.CurrentDirectory}");

			await Task.WhenAny(IntegrationTestSentinel.StartupWorkflowsCompleted.Task, Task.Delay(TimeSpan.FromSeconds(30)));

			await WorkflowScenario.RunAsync(desktop, WorkflowsFileName);

			var bindingErrors = _bindingErrors?.Errors ?? [];
			if (bindingErrors.Length > 0)
				throw new InvalidOperationException($"バインディングエラーが発生しました:\n{string.Join("\n", bindingErrors)}");

			passed = true;
		}
		catch (Exception ex)
		{
			WriteResult($"エラー: {ex}");
			WriteResult("表示中のテキスト:");
			foreach (var window in desktop.Windows)
				WriteResult($"[{window.Title}] {string.Join(" / ", UiOperations.VisibleTexts(window))}");
		}

		WriteResult(passed ? "RESULT: PASSED" : "RESULT: FAILED");
		desktop.Shutdown(passed ? 0 : 1);
	}

	/// <summary>
	/// 実行中のステップを記録する。アプリが応答しなくなっても、どこまで進んだか分かるように都度書き出す
	/// </summary>
	public static void Step(string description)
		=> WriteResult($"STEP: {description}");

	private static void WriteResult(string line)
	{
		lock (ResultLock)
			File.AppendAllText(ResultPath, line + Environment.NewLine);
	}

	/// <summary>
	/// 画面の表示中に発生したバインディングエラーを記録する。例外にならずログへ出るだけなので、ここで拾う
	/// </summary>
	private sealed class BindingErrorSink(ILogSink? inner) : ILogSink
	{
		// 選択前で途中の値が null の状態は正常に起こる。メンバーが消えた場合は別のメッセージになる
		private const string NullSegmentMessage = "Value is null.";

		private readonly List<string> _errors = [];

		public string[] Errors
		{
			get {
				lock (_errors)
					return [.. _errors];
			}
		}

		public bool IsEnabled(LogEventLevel level, string area)
			=> IsBindingError(level, area) || (inner?.IsEnabled(level, area) ?? false);

		public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
			=> Log(level, area, source, messageTemplate, []);

		public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
		{
			if (IsBindingError(level, area) && !propertyValues.Contains(NullSegmentMessage))
			{
				lock (_errors)
					_errors.Add($"{source}: {messageTemplate} [{string.Join(", ", propertyValues)}]");
			}
			if (inner?.IsEnabled(level, area) ?? false)
				inner.Log(level, area, source, messageTemplate, propertyValues);
		}

		private static bool IsBindingError(LogEventLevel level, string area)
			=> area == LogArea.Binding && level >= LogEventLevel.Warning;
	}
}
#endif
