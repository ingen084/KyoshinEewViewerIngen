using System;

namespace KyoshinEewViewer.Notification;

/// <summary>
/// 通知の緊急度
/// </summary>
public enum NotificationUrgency
{
	/// <summary>参考情報 (地震情報ソース変更など)</summary>
	Low,
	/// <summary>通常 (地震情報受信など)</summary>
	Normal,
	/// <summary>重要 (震度5弱以上・津波警報以上)。対応プラットフォームではおやすみモードを貫通し常時表示する</summary>
	Critical,
}

/// <summary>
/// 通知リクエスト
/// </summary>
/// <param name="Title">タイトル</param>
/// <param name="Message">本文</param>
/// <param name="Urgency">緊急度</param>
public record NotificationRequest(string Title, string Message, NotificationUrgency Urgency = NotificationUrgency.Normal);

public abstract class NotificationProvider : IDisposable
{
	/// <summary>
	/// アプリケーション識別子。
	/// Linux の desktop-entry / Windows の AppUserModelID / macOS の CFBundleIdentifier すべてに共通で使用する
	/// </summary>
	public const string ApplicationId = "net.ingen084.kyoshineewviewer";

	/// <summary>
	/// 通知に表示するアプリケーション名
	/// </summary>
	public const string ApplicationName = "KyoshinEewViewer for ingen";

	public abstract void SendNotice(NotificationRequest request);

	/// <summary>
	/// デスクトップ環境への統合を行う (Linux のデスクトップエントリ生成など)。
	/// 統合が不要なプラットフォームでは既定実装のまま何もしない
	/// </summary>
	public virtual void RegisterDesktopIntegration() { }

	public abstract void Dispose();
}
