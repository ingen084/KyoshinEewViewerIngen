using KyoshinEewViewer.Services.TelegramPublishers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 外部APIソース（AXIS, P2P地震情報）用のTelegram
/// BasedTelegram の required 制約を満たすために使用する
/// 表示データは DisplayDataProvider 経由で取得するため GetBodyAsync は使用しない
/// </summary>
internal class ExternalApiTelegram(string source, string title, string eventId, DateTime arrivalTime)
	: Telegram($"{source}:{eventId}:{arrivalTime:yyyyMMddHHmmss}", title, eventId, arrivalTime)
{
	public override void Cleanup() { }

	public override Task<Stream> GetBodyAsync()
		=> throw new NotSupportedException("外部APIソースのTelegramはBodyStreamを提供しません。DisplayDataProviderを使用してください。");
}
