using System;

namespace KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

/// <summary>
/// 防災情報XMLフィード受診時のHEADリクエストに失敗した
/// </summary>
public class HeadFetchErrorException : Exception
{
	public HeadFetchErrorException(string? message) : base(message)
	{
	}
}
