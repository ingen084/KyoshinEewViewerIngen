using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using R3;
using ReactiveUI;
using Sentry;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Unit = System.Reactive.Unit;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace KyoshinEewViewer.Services.Feedback;

public class FeedbackSettingPage : ReactiveObject, ISettingPage
{
	private const string FeedbackCategoryBug = "バグ報告";
	private const long AttachmentMaxTotalSize = 20 * 1024 * 1024; // 合計20MB

	public bool IsVisible => true;
	public string? Icon => "\xf0e0";
	public string Title => "フィードバック";
	public Control DisplayControl => new FeedbackPage() { DataContext = this };
	public ISettingPage[] SubPages => [];

	private KyoshinEewViewerConfiguration Config { get; }
	private ISubWindowsService? SubWindowService { get; }
	private ILogger Logger { get; }

	public string[] Categories { get; } = [FeedbackCategoryBug, "機能要望", "質問", "その他"];

	public bool IsAvailable { get; } = SentrySdk.IsEnabled;

	private string _category = FeedbackCategoryBug;
	public string Category
	{
		get => _category;
		set
		{
			this.RaiseAndSetIfChanged(ref _category, value);
			// 種別切替時は種別に応じた既定値に戻す（バグ報告のみ既定ON）
			IncludeLogs = value == FeedbackCategoryBug;
		}
	}

	private string _subject = "";
	public string Subject
	{
		get => _subject;
		set => this.RaiseAndSetIfChanged(ref _subject, value);
	}

	private string _body = "";
	public string Body
	{
		get => _body;
		set => this.RaiseAndSetIfChanged(ref _body, value);
	}

	private string _email = "";
	public string Email
	{
		get => _email;
		set => this.RaiseAndSetIfChanged(ref _email, value);
	}

	private bool _includeLogs;
	public bool IncludeLogs
	{
		get => _includeLogs;
		set => this.RaiseAndSetIfChanged(ref _includeLogs, value);
	}

	private bool _isSending;
	public bool IsSending
	{
		get => _isSending;
		set => this.RaiseAndSetIfChanged(ref _isSending, value);
	}

	private string? _resultMessage;
	public string? ResultMessage
	{
		get => _resultMessage;
		set => this.RaiseAndSetIfChanged(ref _resultMessage, value);
	}

	public ObservableCollection<FeedbackAttachment> Attachments { get; } = [];

	private string _attachmentsTotalSizeText = "";
	public string AttachmentsTotalSizeText
	{
		get => _attachmentsTotalSizeText;
		private set => this.RaiseAndSetIfChanged(ref _attachmentsTotalSizeText, value);
	}

	public ReactiveUI.ReactiveCommand<Unit, Unit> AddAttachmentCommand { get; }
	public ReactiveUI.ReactiveCommand<FeedbackAttachment, Unit> RemoveAttachmentCommand { get; }
	public ReactiveUI.ReactiveCommand<Unit, Unit> SendCommand { get; }

	public FeedbackSettingPage(
		KyoshinEewViewerConfiguration config,
		ILogManager logManager,
		ISubWindowsService? subWindowService)
	{
		SplatRegistrations.RegisterLazySingleton<FeedbackSettingPage>();

		Config = config;
		SubWindowService = subWindowService;
		Logger = logManager.GetLogger<FeedbackSettingPage>();

		_includeLogs = _category == FeedbackCategoryBug;

		// ReactiveCommand は System.Reactive の IObservable<bool> を要求するため変換する
		var canSend = Observable.CombineLatest(
			this.ObservePropertyChanged(x => x.Subject),
			this.ObservePropertyChanged(x => x.Body),
			this.ObservePropertyChanged(x => x.IsSending),
			(subject, body, sending) => IsAvailable && !sending
				&& !string.IsNullOrWhiteSpace(subject)
				&& !string.IsNullOrWhiteSpace(body)).AsSystemObservable();

		AddAttachmentCommand = ReactiveCommand.CreateFromTask(AddAttachment);
		RemoveAttachmentCommand = ReactiveCommand.Create<FeedbackAttachment>(RemoveAttachment);
		SendCommand = ReactiveCommand.CreateFromTask(Send, canSend);

		Attachments.CollectionChanged += (_, _) => UpdateAttachmentsTotalSizeText();
		UpdateAttachmentsTotalSizeText();
	}

	private void UpdateAttachmentsTotalSizeText()
	{
		var total = Attachments.Sum(a => a.Size);
		AttachmentsTotalSizeText = $"合計 {FormatSize(total)} / {FormatSize(AttachmentMaxTotalSize)}";
	}

	private static string FormatSize(long size) => size switch
	{
		< 1024 => $"{size} B",
		< 1024 * 1024 => $"{size / 1024.0:0.##} KB",
		_ => $"{size / (1024.0 * 1024.0):0.##} MB",
	};

	private async Task AddAttachment()
	{
		if (KyoshinEewViewerApp.TopLevelControl == null)
			return;
		var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
		{
			Title = "添付するファイルを選択",
			FileTypeFilter = [FilePickerFileTypes.All],
			AllowMultiple = true,
		});
		if (files is not { Count: > 0 })
			return;

		var currentTotal = Attachments.Sum(a => a.Size);
		var rejectedOversize = new List<string>();
		var failed = new List<string>();
		foreach (var file in files)
		{
			if (file.TryGetLocalPath() is not { } localPath)
				continue;
			try
			{
				var info = new FileInfo(localPath);
				if (!info.Exists)
					continue;
				if (Attachments.Any(a => a.FilePath == localPath))
					continue;
				if (currentTotal + info.Length > AttachmentMaxTotalSize)
				{
					rejectedOversize.Add(info.Name);
					continue;
				}
				Attachments.Add(new FeedbackAttachment(localPath, info.Name, info.Length));
				currentTotal += info.Length;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, $"添付候補ファイルの読み取りに失敗しました: {localPath}");
				failed.Add(Path.GetFileName(localPath));
			}
		}

		if (rejectedOversize.Count > 0 || failed.Count > 0)
		{
			var sb = new StringBuilder();
			if (rejectedOversize.Count > 0)
			{
				sb.AppendLine($"合計サイズが上限（{AttachmentMaxTotalSize / (1024 * 1024)}MB）を超えるため添付できなかったファイル:");
				foreach (var name in rejectedOversize)
					sb.AppendLine($"・{name}");
			}
			if (failed.Count > 0)
			{
				if (sb.Length > 0)
					sb.AppendLine();
				sb.AppendLine("読み取りに失敗したファイル:");
				foreach (var name in failed)
					sb.AppendLine($"・{name}");
			}

			await new FAContentDialog
			{
				Title = "添付できないファイルがあります",
				Content = sb.ToString().TrimEnd(),
				CloseButtonText = "OK",
			}.ShowAsync(SubWindowService?.SettingWindow);
		}
	}

	private void RemoveAttachment(FeedbackAttachment attachment)
		=> Attachments.Remove(attachment);

	private async Task Send()
	{
		if (!SentrySdk.IsEnabled)
		{
			ResultMessage = "フィードバック機能を利用できません。";
			return;
		}

		IsSending = true;
		ResultMessage = "送信中...";
		try
		{
			var subject = Subject.Trim();
			var body = Body.Trim();
			var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
			var category = Category;
			var includeLogs = IncludeLogs;
			var attachments = Attachments.ToArray();

			var version = Core.Utils.Version.Replace("\n", "-");
			var instanceId = Config.InstanceId.ToString();

			// フィードバックエンベロープは添付ファイルを1件しか含められないため、
			// ログ添付有効・添付ファイル2件以上の場合は先にイベントとして送信し、
			// feedback から参照させる
			var useAssociatedEvent = includeLogs || attachments.Length >= 2;

			byte[]? logBytes = null;
			if (includeLogs)
			{
				var provider = Locator.Current.GetService<InMemoryLoggerProvider>();
				if (provider != null)
					logBytes = Encoding.UTF8.GetBytes(FormatLogsForAttachment(provider.GetLogs()));
			}

			await Task.Run(() =>
			{
				SentryId? associatedEventId = null;
				if (useAssociatedEvent)
				{
					var eventId = SentrySdk.CaptureMessage(
						$"[Feedback:{category}] {subject}",
						scope =>
						{
							scope.Level = SentryLevel.Info;
							scope.SetTag("feedback.category", category);
							scope.SetTag("app.version", version);
							scope.SetExtra("feedback.instance_id", instanceId);

							if (logBytes is { Length: > 0 })
								scope.AddAttachment(logBytes, "recent-logs.txt", AttachmentType.Default, "text/plain");
							foreach (var a in attachments)
								scope.AddAttachment(a.FilePath);
						},
						SentryLevel.Info);
					if (eventId != SentryId.Empty)
						associatedEventId = eventId;
				}

				var message = new StringBuilder();
				message.AppendLine($"[{category}] {subject}");
				message.AppendLine();
				message.AppendLine(body);
				var feedback = new SentryFeedback(
					message: message.ToString(),
					contactEmail: email,
					name: null,
					replayId: null,
					url: null,
					associatedEventId: associatedEventId);

				SentrySdk.CaptureFeedback(feedback, scope =>
				{
					scope.SetTag("feedback.category", category);
					scope.SetTag("app.version", version);
					scope.SetExtra("feedback.instance_id", instanceId);

					// 添付ファイルが1件だけで関連イベントを作っていない場合は
					// フィードバックエンベロープに直接添付する
					if (!useAssociatedEvent && attachments.Length == 1)
						scope.AddAttachment(attachments[0].FilePath);
				});
				SentrySdk.Flush(TimeSpan.FromSeconds(10));
			});

			Logger.LogInfo($"フィードバックを送信しました ({category})");
			Subject = "";
			Body = "";
			Email = "";
			Attachments.Clear();
			ResultMessage = null;

			await new FAContentDialog
			{
				Title = "フィードバック送信完了",
				Content = "フィードバックを送信しました。ご協力ありがとうございました。",
				CloseButtonText = "OK",
			}.ShowAsync(SubWindowService?.SettingWindow);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "フィードバックの送信に失敗しました");
			ResultMessage = $"送信に失敗しました: {ex.Message}";
		}
		finally
		{
			IsSending = false;
		}
	}

	private static string FormatLogsForAttachment(IEnumerable<LogEntry> logs)
	{
		var sb = new StringBuilder();
		foreach (var log in logs)
		{
			sb.Append(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
			sb.Append(" [");
			sb.Append(log.LogLevel);
			sb.Append("] ");
			sb.Append(log.CategoryName);
			sb.Append(": ");
			sb.AppendLine(log.Message);
			if (log.Exception != null)
				sb.AppendLine(log.Exception.ToString());
		}
		return sb.ToString();
	}
}

public record class FeedbackAttachment(string FilePath, string FileName, long Size)
{
	public string SizeText => Size switch
	{
		< 1024 => $"{Size} B",
		< 1024 * 1024 => $"{Size / 1024.0:0.##} KB",
		_ => $"{Size / (1024.0 * 1024.0):0.##} MB",
	};
}
