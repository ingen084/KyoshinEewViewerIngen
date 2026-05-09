using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinEewViewer.Series.Tsunami.Events;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SlackBot;
public class MisskeyUploader
{
	private string? MisskeyServer { get; } = Environment.GetEnvironmentVariable("MISSKEY_SERVER_HOST");
	private string? AccessKey { get; } = Environment.GetEnvironmentVariable("MISSKEY_ACCESS_KEY");

	public string? KyoshinMonitorFolderId { get; } = Environment.GetEnvironmentVariable("MISSKEY_DRIVE_FOLDER_ID_KMONI");
	public string? EarthquakeFolderId { get; } = Environment.GetEnvironmentVariable("MISSKEY_DRIVE_FOLDER_ID_EQ");
	public string? TsunamiFolderId { get; } = Environment.GetEnvironmentVariable("MISSKEY_DRIVE_FOLDER_ID_TSUNAMI");

	private HttpClient Client { get; } = new();
	private ILogger Logger { get; }

	public MisskeyUploader()
	{
		Logger = Locator.Current.RequireService<ILogManager>().GetLogger<MisskeyUploader>();
		if (MisskeyServer is null || AccessKey is null)
			Logger.LogWarning("環境変数 MISSKEY_SERVER_HOST または MISSKEY_ACCESS_KEY が設定されていないため、Misskeyへの投稿ができません。");
	}

	/// <summary>
	/// イベントとスレッドのマッピング
	/// </summary>
	private Dictionary<string, string?> EventMap { get; } = [];

	public Task UploadTest(Task<CaptureResult> captureTask)
		=> Upload(null, "画像投稿のテスト", null, false, captureTask, EarthquakeFolderId, null);

	public async Task UploadTsunamiInformation(TsunamiInformationUpdated x, Task<CaptureResult>? captureTask, TaskCompletionSource<string?>? imageUrlSource)
	{
		var oldLevelStr = x.Current?.Level switch
		{
			TsunamiLevel.MajorWarning => "大津波警報",
			TsunamiLevel.Warning => "津波警報",
			TsunamiLevel.Advisory => "津波注意報",
			TsunamiLevel.Forecast => "津波予報",
			_ => "",
		};
		var levelStr = x.New?.Level switch
		{
			TsunamiLevel.MajorWarning => "大津波警報",
			TsunamiLevel.Warning => "津波警報",
			TsunamiLevel.Advisory => "津波注意報",
			TsunamiLevel.Forecast => "津波予報",
			_ => "",
		};
		var title = "**津波情報** 更新";
		var message = "津波情報が更新されました。";

		// 発表
		if (
			(x.Current == null || x.Current.Level <= TsunamiLevel.None) && x.New != null &&
			(
				x.New.AdvisoryAreas != null ||
				x.New.ForecastAreas != null ||
				x.New.MajorWarningAreas != null ||
				x.New.WarningAreas != null
			)
		)
		{
			title = $"**{levelStr}** 発表";
			message = $"{levelStr}が発表されました。";
		}
		// 解除
		else if (x.Current != null && x.Current.Level > TsunamiLevel.None && (x.New == null || x.New.Level < x.Current.Level))
		{
			if (x.Current.Level == TsunamiLevel.Forecast)
				title = "津波予報 期限切れ";
			else
				title = $"**{levelStr}** 発表中";
			message = x.New?.Level switch
			{
				TsunamiLevel.MajorWarning => "大津波警報が引き続き発表されています。",
				TsunamiLevel.Warning => "大津波警報は津波警報に切り替えられました。",
				TsunamiLevel.Advisory => "津波警報は津波注意報に切り替えられました。",
				TsunamiLevel.Forecast => "津波警報・注意報は予報に切り替えられました。",
				_ => x.Current.Level == TsunamiLevel.Forecast ? "津波予報の情報期限が切れました。" : "津波警報・注意報・予報は解除されました。",
			};
		}
		// 引き上げ
		else if (x.Current != null && x.New != null && x.Current.Level < x.New.Level)
		{
			title = $"**{levelStr}** 切り替え";
			message = $"{oldLevelStr}は、" + (x.New.Level switch
			{
				TsunamiLevel.MajorWarning => "大津波警報に切り替えられました。",
				TsunamiLevel.Warning => "津波警報に切り替えられました。",
				TsunamiLevel.Advisory => "津波注意報に切り替えられました。",
				TsunamiLevel.Forecast => "津波予報が発表されています。",
				_ => "", // 存在しないはず
			});
		}

		await Upload(
			x.Current?.EventId ?? x.New?.EventId,
			$"$[scale.x=1.2,y=1.2 　🌊 {title}]\n\n{message}",
			null,
			true,
			captureTask,
			TsunamiFolderId,
			imageUrlSource
		);
	}

	public async Task UploadEarthquakeInformation(EarthquakeInformationUpdated x, Task<CaptureResult>? captureTask, TaskCompletionSource<string?>? imageUrlSource)
	{
		var markdown = new StringBuilder();

		if (x.Earthquake.IsTraining)
			markdown.Append("$[x2 **これは訓練です**]\n\n");

		markdown.Append($"$[scale.x=1.2,y=1.2 　ℹ️ ");
		if (x.Earthquake.Intensity != JmaIntensity.Unknown)
		{
			var (bp, fp, _) = FixedObjectRenderer.IntensityPaintCache[x.Earthquake.Intensity];
			markdown.Append($"$[bg.color={bp.Color.ToString()[3..]} $[fg.color={fp.Color.ToString()[3..]}  **最大{x.Earthquake.Intensity.ToLongString()}** ]] ");
		}
		markdown.Append($"**{x.Earthquake.Title}**]\n");

		if (x.Earthquake.IsHypocenterAvailable)
		{
			markdown.Append($"{x.Earthquake.Time:d日H時m分}<small>頃発生</small>\n<small>震源</small>**{x.Earthquake.Place ?? "不明"}**");
			if (!x.Earthquake.IsNoDepthData)
			{
				markdown.Append("/<small>深さ</small>");
				if (x.Earthquake.IsVeryShallow)
					markdown.Append("**ごく浅い**");
				else
					markdown.Append($"**{x.Earthquake.Depth}km**");
			}
			markdown.Append($"/<small>規模</small>**{x.Earthquake.MagnitudeAlternativeText ?? $"M{x.Earthquake.Magnitude:0.0}"}**\n");
		}

		if (!string.IsNullOrWhiteSpace(x.Earthquake.Comment))
			markdown.Append($"\n{x.Earthquake.Comment}");

		await Upload(
			x.Earthquake.EventId,
			markdown.ToString(),
			null,
			true,
			captureTask,
			EarthquakeFolderId,
			imageUrlSource
		);
	}

	public async Task UploadShakeDetected(KyoshinShakeDetected x, Task<CaptureResult>? captureTask, TaskCompletionSource<string?>? imageUrlSource)
	{
		// 震度1未満の揺れは処理しない
		if (x.Event.Level <= KyoshinEventLevel.Weak)
			return;

		var topPoint = x.Event.Points.OrderByDescending(p => p.LatestIntensity).First();

		var maxIntensity = topPoint.LatestIntensity.ToJmaIntensity();

		var msg = x.Event.Level switch
		{
			KyoshinEventLevel.Weaker => "微弱な",
			KyoshinEventLevel.Weak => "弱い",
			KyoshinEventLevel.Medium => "",
			KyoshinEventLevel.Strong => "強い",
			KyoshinEventLevel.Stronger => "非常に強い",
			_ => "",
		} + "揺れを検知しました。";

		await Upload(
			x.Event.Id.ToString(),
			$"$[bg.color={x.Event.Points.OrderByDescending(p => p.LatestIntensity).First().LatestColor?.ToString()[3..] ?? "black"}  ] **{msg}**",
			null,
			x.Event.Level > KyoshinEventLevel.Medium,
			captureTask,
			KyoshinMonitorFolderId,
			imageUrlSource
		);
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	public JsonSerializerOptions GetOptions() => new(JsonSerializerOptions.Default) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, TypeInfoResolver = MisskeySerializerContext.Default };

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
	public async Task Upload(string? eventId, string text, string? cw, bool isPublic, Task<CaptureResult>? captureTask, string? imageFolderId, TaskCompletionSource<string?>? imageUrlSource)
	{
		try
		{
			if (AccessKey is null || MisskeyServer is null)
				return;

			var totalStopwatch = Stopwatch.StartNew();
			string? fileId = null;
			CaptureResult? captureResult = null;
			try
			{
				if (captureTask != null)
				{
					captureResult = await captureTask;

					var fileName = $"{DateTime.Now:yyyyMMddHHmmssffff}.webp";
					var fileContent = new ByteArrayContent(captureResult.Data);
					fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");

					// Misskey (@fastify/multipart) は request.file() で最初のファイルを検出すると
					// それ以降のフィールドが fields に載らないことがあるため、file パートは最後に追加する
					using var data = new MultipartFormDataContent {
						{ new StringContent(AccessKey), "i" },
						{ new StringContent(fileName), "name" },
					};

					if (imageFolderId != null)
						data.Add(new StringContent(imageFolderId), "folderId");

					data.Add(fileContent, "file", fileName);

					totalStopwatch.Restart();
					var response = await Client.PostAsync($"https://{MisskeyServer}/api/drive/files/create", data);
					if (response.IsSuccessStatusCode)
					{
						var driveFile = await JsonSerializer.DeserializeAsync(await response.Content.ReadAsStreamAsync(), MisskeySerializerContext.Default.DriveFile);
						fileId = driveFile?.Id;
						imageUrlSource?.TrySetResult(driveFile?.Url);
					}
					else
					{
						Logger.LogWarning($"ファイルのアップロードに失敗しました({response.StatusCode})\n{await response.Content.ReadAsStringAsync()}");
						imageUrlSource?.TrySetResult(null);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "ファイルのアップロードに失敗しました");
				imageUrlSource?.TrySetResult(null);
			}
			var uploadFile = totalStopwatch.Elapsed;

			string? noteId = null;
			try
			{
				string? replyId = null;
				if (eventId != null)
					EventMap.TryGetValue(eventId, out replyId);

				var response = await Client.PostAsync(
					$"https://{MisskeyServer}/api/notes/create",
					new StringContent(
						JsonSerializer.Serialize(new PostingNote
						{
							I = AccessKey,
							Text = text,
							Cw = cw,
							ReplyId = replyId,
							FileIds = fileId != null ? [fileId] : null,
							Visibility = isPublic ? "public" : "home",
						}, GetOptions()),
						Encoding.UTF8, "application/json"));
				if (response.IsSuccessStatusCode)
				{
					noteId = (await JsonSerializer.DeserializeAsync(await response.Content.ReadAsStreamAsync(), MisskeySerializerContext.Default.CreateNoteResponse))?.CreatedNote?.Id;
					if (eventId != null && noteId != null)
						EventMap[eventId] = noteId;
					Logger.LogInfo($"ノートを投稿しました: {noteId}");
				}
				else
					Logger.LogWarning($"ノートの投稿に失敗しました({response.StatusCode})\n{await response.Content.ReadAsStringAsync()}");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "ノートの投稿に失敗しました");
			}
			var postNote = totalStopwatch.Elapsed;

			if (captureResult == null || noteId == null)
				return;

			try
			{
				var response = await Client.PostAsync(
					$"https://{MisskeyServer}/api/notes/create",
					new StringContent(
						JsonSerializer.Serialize(new PostingNote
						{
							I = AccessKey,
							Text = @$"**パフォーマンス情報**
```
Total: {postNote.TotalMilliseconds:0.000}ms
├Capture : {captureResult.TotalTime.TotalMilliseconds:0.000}ms
│├Measure: {captureResult.MeasureTime.TotalMilliseconds:0.000}ms
│├Arrange: {captureResult.ArrangeTime.TotalMilliseconds:0.000}ms
│├Render : {captureResult.RenderTime.TotalMilliseconds:0.000}ms
│└Save   : {captureResult.SaveTime.TotalMilliseconds:0.000}ms
├Upload : {uploadFile.TotalMilliseconds:0.000}ms
└Post   : {(postNote - uploadFile).TotalMilliseconds:0.000}ms
```",
							ReplyId = noteId,
							Visibility = "home",
							LocalOnly = true,
						},
						new JsonSerializerOptions(JsonSerializerOptions.Default) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, TypeInfoResolver = MisskeySerializerContext.Default }),
						Encoding.UTF8, "application/json"));
				if (response.IsSuccessStatusCode)
				{
					var noteId2 = (await JsonSerializer.DeserializeAsync(await response.Content.ReadAsStreamAsync(), MisskeySerializerContext.Default.CreateNoteResponse))?.CreatedNote?.Id;
					Logger.LogInfo($"ノートを投稿しました: {noteId2}");
				}
				else
					Logger.LogWarning($"ノートの投稿に失敗しました({response.StatusCode})\n{await response.Content.ReadAsStringAsync()}");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "ノートの投稿に失敗しました");
			}
		}
		finally
		{
			// 全経路で必ず一度は通知する保険。既に TrySetResult 済みなら no-op。
			imageUrlSource?.TrySetResult(null);
		}
	}


}

[JsonSerializable(typeof(DriveFile))]
[JsonSerializable(typeof(PostingNote))]
[JsonSerializable(typeof(CreateNoteResponse))]
[JsonSerializable(typeof(CreatedNote))]
public partial class MisskeySerializerContext : JsonSerializerContext
{
}

public class DriveFile
{
	[JsonPropertyName("id")]
	public string? Id { get; set; } = "";
	[JsonPropertyName("url")]
	public string? Url { get; set; } = "";
}

public class PostingNote
{
	[JsonPropertyName("i")]
	public string? I { get; init; }
	[JsonPropertyName("text")]
	public string? Text { get; init; }
	[JsonPropertyName("cw")]
	public string? Cw { get; init; }
	[JsonPropertyName("fileIds")]
	public string[]? FileIds { get; init; }
	[JsonPropertyName("replyId")]
	public string? ReplyId { get; init; }
	[JsonPropertyName("visibility")]
	public string Visibility { get; set; } = "home"; // 正式公開するときはこれを変更する
	[JsonPropertyName("localOnly")]
	public bool? LocalOnly { get; init; }
}

public class CreateNoteResponse
{
	[JsonPropertyName("createdNote")]
	public CreatedNote? CreatedNote { get; set; }
}
public class CreatedNote
{
	[JsonPropertyName("id")]
	public string? Id { get; set; }
}
