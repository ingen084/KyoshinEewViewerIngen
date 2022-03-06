using System;

namespace KyoshinEewViewer.Services.TelegramPublishers;

/// <summary>
/// 電文の取得に失敗した
/// </summary>
public class TelegramFetchFailedException : Exception
{
	public TelegramFetchFailedException(string message) : base(message)
	{ }
}
