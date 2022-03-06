using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

/// <summary>
/// フィードの取得状況などを保持する
/// </summary>
internal class FeedContext
{
	public DateTimeOffset? LongFeedLastModified { get; set; }
	public DateTimeOffset? ShortFeedLastModified { get; set; }
	public DateTime LastFetched { get; set; } = DateTime.MinValue;
	public List<Telegram> LatestTelegrams { get; } = new();
}
