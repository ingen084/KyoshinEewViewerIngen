using KyoshinEewViewer.Core;
using Splat;
using System;
using System.Collections.Generic;
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

	HttpClient Client { get; } = new();
	private ILogger Logger { get; }

	public MisskeyUploader()
	{
		Logger = Locator.Current.RequireService<ILogManager>().GetLogger<MisskeyUploader>();
	}

	/// <summary>
	/// イベントとスレッドのマッピング
	/// </summary>
	private Dictionary<string, string?> EventMap { get; } = new();

	public Task UploadTest(Task<byte[]> captureTask)
		=> Upload(null, "画像投稿のテスト", null, captureTask, EarthquakeFolderId);

	public async Task Upload(string? eventId, string text, string? cw, Task<byte[]>? captureTask = null, string? imageFolderId = null)
	{
		if (AccessKey is null || MisskeyServer is null)
			return;

		string? fileId = null;
		try
		{
			if (captureTask != null)
			{
				var fileName = $"{DateTime.Now:yyyyMMddHHmmssffff}.png";

				using var data = new MultipartFormDataContent {
					{ new StringContent(AccessKey), "i" },
					{ new ByteArrayContent(await captureTask), "file", fileName },
					{ new StringContent(fileName), "name" },
				};

				if (imageFolderId != null)
					data.Add(new StringContent(imageFolderId), "folderId");

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
							FileIds = fileId != null ? new[] { fileId } : null,
						},
						new JsonSerializerOptions(JsonSerializerOptions.Default) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
					Encoding.UTF8, "application/json"));
			if (response.IsSuccessStatusCode)
			{
				var noteId = (await JsonSerializer.DeserializeAsync<PostedNote>(await response.Content.ReadAsStreamAsync()))?.Id;
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
	}

	public class PostedNote
	{
		[JsonPropertyName("id")]
		public string? Id { get; set; }
	}
}
