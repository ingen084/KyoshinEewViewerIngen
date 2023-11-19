using System;

namespace KyoshinEewViewer.Services.TelegramPublishers.JmaXml;

/// <summary>
/// 防災情報XMLフィード受診時のHEADリクエストに失敗した
/// </summary>
public class HeadFetchErrorException(string? message) : Exception(message)
{
}
