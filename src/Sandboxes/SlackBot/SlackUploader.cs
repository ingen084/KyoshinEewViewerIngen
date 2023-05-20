using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinMonitorLib;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.WebApi;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Splat.ILogger;

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
	private ILogger Logger { get; }

    public SlackUploader()
    {
        ApiClient = new SlackServiceBuilder().UseApiToken(ApiToken).GetApiClient();
        Logger = Locator.Current.RequireService<ILogManager>().GetLogger<SlackUploader>();
    }

    public async Task UploadEarthquakeInformation(EarthquakeInformationUpdated x, Task<byte[]>? captureTask = null)
    {
	    var headerKvp = new Dictionary<string, string>();

	    if (x.Earthquake.IsHypocenterAvailable)
	    {
		    headerKvp.Add("震央", x.Earthquake.Place ?? "不明");

		    if (!x.Earthquake.IsNoDepthData)
		    {
			    if (x.Earthquake.IsVeryShallow)
				    headerKvp.Add("震源の深さ", "ごく浅い");
			    else
				    headerKvp.Add("震源の深さ", x.Earthquake.Depth + "km");
		    }

		    headerKvp.Add("規模", x.Earthquake.MagnitudeAlternativeText ?? $"M{x.Earthquake.Magnitude:0.0}");
	    }

	    await Upload(
		    x.Earthquake.Id,
		    $"#{FixedObjectRenderer.IntensityPaintCache[x.Earthquake.Intensity].b.Color.ToString()[3..]}",
		    $":information_source: {x.Earthquake.Title} 最大{x.Earthquake.Intensity.ToLongString()}",
		    $"【{x.Earthquake.Title}】{x.Earthquake.GetNotificationMessage()}",
		    mrkdwn: x.Earthquake.HeadlineText,
		    headerKvp: headerKvp,
		    footerMrkdwn: x.Earthquake.Comment,
		    captureTask: captureTask
	    );
    }

	public async Task UploadShakeDetected(KyoshinShakeDetected x, Task<byte[]>? captureTask = null)
    {
	    // 震度1未満の揺れは処理しない
	    if (x.Event.Level <= KyoshinEventLevel.Weak)
		    return;

	    var topPoint = x.Event.Points.OrderByDescending(p => p.LatestIntensity).First();
	    var markdown = new StringBuilder($"*最大{topPoint.LatestIntensity.ToJmaIntensity().ToLongString()}* ({topPoint.LatestIntensity:0.0})");
	    var prefGroups = x.Event.Points.OrderByDescending(p => p.LatestIntensity).GroupBy(p => p.Region);
	    foreach (var group in prefGroups)
		    markdown.Append($"\n  {group.Key}: {group.First().LatestIntensity.ToJmaIntensity().ToLongString()}({group.First().LatestIntensity:0.0})");

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
		    "#" + (topPoint.LatestColor?.ToString()[3..] ?? "FFF"),
		    ":warning: " + msg,
		    "【地震情報】" + msg,
		    mrkdwn: markdown.ToString(),
		    captureTask: captureTask
	    );
    }

	public async Task Upload(string? eventId, string color, string title, string noticeText, string? mrkdwn = null, string? footerMrkdwn = null, Dictionary<string, string>? headerKvp = null, Dictionary<string, string>? contentKvp = null, Task<byte[]>? captureTask = null)
    {
	    try
	    {
		    var parentTs = eventId == null ? null : EventMap.TryGetValue(eventId, out var ts) ? ts : null;
		    // 本文のコンテンツを組み立てる
		    var message = new Message {
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

		    parentTs ??= postedMessage.Ts;

		    if (eventId != null && !EventMap.ContainsKey(eventId))
			    EventMap[eventId] = postedMessage.Ts;

		    if (captureTask == null)
			    return;

		    var imageData = await captureTask;
		    var file = await ApiClient.Files.Upload(imageData, "png", threadTs: parentTs,
			    channels: new[] { ChannelId });
		    message.Attachments.Insert(0, new Attachment { Text = noticeText, ImageUrl = file.File.UrlPrivate, });

		    // 画像付きのデータで更新
		    await ApiClient.Chat.Update(new MessageUpdate {
			    ChannelId = ChannelId, Ts = postedMessage.Ts, Attachments = message.Attachments,
		    });
	    }
	    catch (Exception ex)
	    {
		    Logger.LogError(ex, "Slack へのアップロード中にエラーが発生しました");
	    }
    }
}
