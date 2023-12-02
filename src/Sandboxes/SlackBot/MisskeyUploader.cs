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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
	}

	/// <summary>
	/// イベントとスレッドのマッピング
	/// </summary>
	private Dictionary<string, string?> EventMap { get; } = [];

	public Task UploadTest(Task<CaptureResult> captureTask)
		=> Upload(null, "画像投稿のテスト", null, false, captureTask, EarthquakeFolderId);

	public async Task UploadTsunamiInformation(TsunamiInformationUpdated x, Task<CaptureResult>? captureTask = null)
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
				TsunamiLevel.Warning => "大津波警報は津波警報に引き下げられました。",
				TsunamiLevel.Advisory => "津波警報は津波注意報に引き下げられました。",
				TsunamiLevel.Forecast => "津波警報・注意報は予報に引き下げられました。",
				_ => x.Current.Level == TsunamiLevel.Forecast ? "津波予報の情報期限が切れました。" : "津波警報・注意報・予報は解除されました。",
			};
		}
		// 引き上げ
		else if (x.Current != null && x.New != null && x.Current.Level < x.New.Level)
		{
			title = $"**{levelStr}** 引き上げ";
			message = $"{oldLevelStr}は、" + (x.New.Level switch
			{
				TsunamiLevel.MajorWarning => "大津波警報に引き上げられました。",
				TsunamiLevel.Warning => "津波警報に引き上げられました。",
				TsunamiLevel.Advisory => "津波注意報に引き上げられました。",
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
			TsunamiFolderId
		);
	}

	public async Task UploadEarthquakeInformation(EarthquakeInformationUpdated x, Task<CaptureResult>? captureTask = null)
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
			markdown.Append($"{x.Earthquake.OccurrenceTime:d日H時m分}<small>頃発生</small>\n<small>震源</small>**{x.Earthquake.Place ?? "不明"}**");
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
			x.Earthquake.Id,
			markdown.ToString(),
			null,
			true,
			captureTask,
			EarthquakeFolderId
		);
	}

	public async Task UploadShakeDetected(KyoshinShakeDetected x, Task<CaptureResult>? captureTask = null)
	{
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
			x.Event.Level >= KyoshinEventLevel.Medium,
			captureTask,
			KyoshinMonitorFolderId
		);
	}

	public async Task Upload(string? eventId, string text, string? cw, bool isPublic = false, Task<CaptureResult>? captureTask = null, string? imageFolderId = null)
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

				var fileName = $"{DateTime.Now:yyyyMMddHHmmssffff}.png";
				using var data = new MultipartFormDataContent {
					{ new StringContent(AccessKey), "i" },
					{ new ByteArrayContent(captureResult.Data), "file", fileName },
					{ new StringContent(fileName), "name" },
				};

				if (imageFolderId != null)
					data.Add(new StringContent(imageFolderId), "folderId");

				totalStopwatch.Restart();
				var response = await Client.PostAsync($"https://{MisskeyServer}/api/drive/files/create", data);
				if (response.IsSuccessStatusCode)
					fileId = (await JsonSerializer.DeserializeAsync<DriveFile>(await response.Content.ReadAsStreamAsync()))?.Id;
				else
					Logger.LogWarning($"ファイルのアップロードに失敗しました({response.StatusCode})\n{await response.Content.ReadAsStringAsync()}");
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "ファイルのアップロードに失敗しました");
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
					},
					new JsonSerializerOptions(JsonSerializerOptions.Default) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
					Encoding.UTF8, "application/json"));
			if (response.IsSuccessStatusCode)
			{
				noteId = (await JsonSerializer.DeserializeAsync<CreateNoteResponse>(await response.Content.ReadAsStreamAsync()))?.CreatedNote?.Id;
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
					new JsonSerializerOptions(JsonSerializerOptions.Default) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
					Encoding.UTF8, "application/json"));
			if (response.IsSuccessStatusCode)
			{
				var noteId2 = (await JsonSerializer.DeserializeAsync<CreateNoteResponse>(await response.Content.ReadAsStreamAsync()))?.CreatedNote?.Id;
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

	public class DriveFile
	{
		[JsonPropertyName("id")]
		public string? Id { get; set; } = "";
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
}
