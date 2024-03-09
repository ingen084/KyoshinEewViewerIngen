using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinEewViewer.Series.Tsunami.Events;
using KyoshinEewViewer.Series.Tsunami.Models;
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

public class SlackUploader(string apiToken, string channelId)
{
	private string ChannelId { get; } = channelId;

	/// <summary>
	/// イベントとスレッドのマッピング
	/// </summary>
	private Dictionary<string, string> EventMap { get; } = [];

	private ISlackApiClient ApiClient { get; } = new SlackServiceBuilder().UseApiToken(apiToken).GetApiClient();
	private ILogger Logger { get; } = Locator.Current.RequireService<ILogManager>().GetLogger<SlackUploader>();

	public async Task UploadTsunamiInformation(TsunamiInformationUpdated x, System.Threading.Channels.Channel<string?>? imageUploadedChannel = null)
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
				title = $"{levelStr} 発表中";
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
			"#4169e1",
			$":ocean: {title}",
			$"【津波情報】{message}",
			mrkdwn: message,
			imageUploadedChannel: imageUploadedChannel
		);
	}

	public async Task UploadEarthquakeInformation(EarthquakeInformationUpdated x, System.Threading.Channels.Channel<string?>? imageUploadedChannel = null)
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
		    x.Earthquake.EventId,
		    $"#{FixedObjectRenderer.IntensityPaintCache[x.Earthquake.Intensity].Background.Color.ToString()[3..]}",
		    $":information_source: 最大{x.Earthquake.Intensity.ToLongString()} {x.Earthquake.Title}",
		    $"【{x.Earthquake.Title}】{x.Earthquake.GetNotificationMessage()}",
		    // mrkdwn: x.Earthquake.HeadlineText,
		    headerKvp: headerKvp,
		    footerMrkdwn: x.Earthquake.Comment,
			imageUploadedChannel: imageUploadedChannel
		);
    }

	public async Task UploadShakeDetected(KyoshinShakeDetected x, System.Threading.Channels.Channel<string?>? imageUploadedChannel = null)
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
			imageUploadedChannel: imageUploadedChannel
		);
    }

	public async Task Upload(string? eventId, string color, string title, string noticeText, string? mrkdwn = null, string? footerMrkdwn = null, Dictionary<string, string>? headerKvp = null, Dictionary<string, string>? contentKvp = null, System.Threading.Channels.Channel<string?>? imageUploadedChannel = null)
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

		    if (imageUploadedChannel == null ||
				!await imageUploadedChannel.Reader.WaitToReadAsync() ||
				!imageUploadedChannel.Reader.TryRead(out var imageUrl))
			    return;

			//var file = await ApiClient.Files.Upload((await captureTask).Data, "webp", threadTs: parentTs,
			// channels: new[] { ChannelId });

			//Logger.LogInfo($"url_private: {file.File.UrlPrivate} url_private_download: {file.File.UrlPrivateDownload} url_private_download: {file.File.Permalink} permalink_public: {file.File.PermalinkPublic}");
			message.Attachments.Insert(0, new Attachment { Text = noticeText, ImageUrl = imageUrl, });

		    // 画像付きのデータで更新
		    var updatedMessage = await ApiClient.Chat.Update(new MessageUpdate {
			    ChannelId = ChannelId, Ts = postedMessage.Ts, Attachments = message.Attachments,
		    });
			Logger.LogInfo($"Slack へのアップロードが完了しました: {updatedMessage.Channel} {updatedMessage.Ts}");
	    }
	    catch (Exception ex)
	    {
		    Logger.LogError(ex, "Slack へのアップロード中にエラーが発生しました");
	    }
    }
}
