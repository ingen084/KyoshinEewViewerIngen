using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlackBot;

public class SlackUploader
{
	private string ApiToken { get; } = Environment.GetEnvironmentVariable("SLACK_API_TOKEN") ?? throw new Exception("SLACK_API_TOKEN がセットされていません。");
	private string ChannelId { get; } = Environment.GetEnvironmentVariable("SLACK_CHANNEL_ID") ?? throw new Exception("SLACK_CHANNEL_ID がセットされていません。");

	/// <summary>
	/// イベントとスレッドのマッピング
	/// </summary>
	private Dictionary<string, string> EventMap { get; } = new();

	private ISlackApiClient ApiClient { get; }

	public SlackUploader()
	{
		ApiClient = new SlackServiceBuilder().UseApiToken(ApiToken).GetApiClient();
	}


	public async Task Upload(string? eventId, string color, string title, string noticeText, string? mrkdwn = null, string? footerMrkdwn = null, Dictionary<string, string>? headerKvp = null, Dictionary<string, string>? contentKvp = null, Func<Task<byte[]>>? imageCuptureLogic = null)
	{
		// キャプチャを開始しておく
		var captureTask = Task.Run(() => imageCuptureLogic?.Invoke());

		var parentTs = eventId == null ? null : EventMap.TryGetValue(eventId, out var ts) ? ts : null;
		// 本文のコンテンツを組み立てる
		var message = new Message
		{
			Channel = ChannelId,
			Text = noticeText,
			Blocks = new List<Block>(),
			Attachments = new List<Attachment>(),
		};
		var attachment = new Attachment { Color = color, Blocks = new List<Block>() };
		message.Attachments.Add(attachment);

		// タイトル部分
		attachment.Blocks.Add(new HeaderBlock { Text = new(title) });

		// 自由文部分
		if (mrkdwn != null)
			attachment.Blocks.Add(new SectionBlock { Text = new SlackNet.Blocks.Markdown(mrkdwn) });

		// ヘッダ部分
		if (headerKvp?.Any() ?? false)
		{
			var section = new SectionBlock { Fields = new List<TextObject>() };
			foreach (var kvp in headerKvp)
				section.Fields.Add(new SlackNet.Blocks.Markdown($"*{kvp.Key}*\n{kvp.Value}"));
			attachment.Blocks.Add(section);
		}

		// コンテンツ部分
		if (contentKvp?.Any() ?? false)
		{
			foreach (var kvp in contentKvp)
			{
				attachment.Blocks.Add(new HeaderBlock { Text = new(kvp.Key) });
				attachment.Blocks.Add(new SectionBlock { Text = new SlackNet.Blocks.Markdown(kvp.Value) });
			}
		}

		// 末尾自由文部分
		if (footerMrkdwn != null)
			attachment.Blocks.Add(new SectionBlock { Text = new SlackNet.Blocks.Markdown(footerMrkdwn) });

		// イベントIDが存在するばあい
		if (parentTs != null)
		{
			message.ThreadTs = parentTs;
			message.ReplyBroadcast = true;
		}

		var postedMessage = await ApiClient.Chat.PostMessage(message);

		if (parentTs == null)
			parentTs = postedMessage.Ts;

		if (eventId != null && !EventMap.ContainsKey(eventId))
			EventMap[eventId] = postedMessage.Ts;

		if (imageCuptureLogic == null)
			return;

		var imageData = await captureTask;
		var file = await ApiClient.Files.Upload(imageData, "png", threadTs: parentTs, channels: new[] { ChannelId });
		message.Attachments.Insert(0, new Attachment
		{
			Text = "image",
			ImageUrl = file.File.UrlPrivate,
		});

		// 画像付きのデータで更新
		await ApiClient.Chat.Update(new MessageUpdate
		{
			ChannelId = ChannelId,
			Ts = postedMessage.Ts,
			Attachments = message.Attachments,
		});

		//var fileTs = file.File.Shares.Private?.FirstOrDefault().Value?.FirstOrDefault()?.Ts ?? file.File.Shares.Public?.FirstOrDefault().Value?.FirstOrDefault()?.Ts;
		//try
		//{
		//	if (fileTs != null)
		//		await ApiClient.Chat.Delete(fileTs, ChannelId);
		//}
		//catch (Exception ex)
		//{
		//	LoggingService.CreateLogger(this).LogWarning("ファイル投稿の削除に失敗: {ts}\n{ex}", fileTs, ex);
		//}
	}
}
