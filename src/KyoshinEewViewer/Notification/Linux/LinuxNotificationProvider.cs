#if !WINDOWS
using Splat;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace KyoshinEewViewer.Notification.Linux;

public class LinuxNotificationProvider : NotificationProvider
{
	private const string NotificationsService = "org.freedesktop.Notifications";
	private const string NotificationsPath = "/org/freedesktop/Notifications";

	public override bool TrayIconAvailable { get; } = false;

	private DBusConnection? _connection;
	private readonly SemaphoreSlim _connectionLock = new(1, 1);
	private bool _disposed;

	// TODO Linux向けのトレイアイコン処理は未実装
	public override void InitializeTrayIcon(TrayMenuItem[] menuItems) { }

	public override void SendNotice(NotificationRequest request)
		// D-Bus 送信は非同期だが通知はベストエフォートのため待たずに投げる
		=> _ = SendNoticeAsync(request);

	private async Task SendNoticeAsync(NotificationRequest request)
	{
		try
		{
			var connection = await GetConnectionAsync();
			// MessageWriter は ref struct のため await をまたげない。構築は同期メソッドで完結させる
			await connection.CallMethodAsync(BuildNotifyMessage(connection, request));
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "D-Bus通知に失敗したため notify-send にフォールバックします");
			FallbackNotifySend(request);
		}
	}

	private static MessageBuffer BuildNotifyMessage(DBusConnection connection, NotificationRequest request)
	{
		using var writer = connection.GetMessageWriter();
		writer.WriteMethodCallHeader(
			destination: NotificationsService,
			path: NotificationsPath,
			@interface: NotificationsService,
			member: "Notify",
			signature: "susssasa{sv}i",
			flags: MessageFlags.None);
		writer.WriteString(ApplicationName);      // app_name
		writer.WriteUInt32(0);                    // replaces_id (差し替えは未対応)
		writer.WriteString(ApplicationId);        // app_icon (hicolor から名前解決するアイコン名)
		writer.WriteString(request.Title);        // summary
		writer.WriteString(request.Message);      // body
		writer.WriteArray(Array.Empty<string>()); // actions (クリック動作は未対応)

		// hints a{sv}
		var hints = writer.WriteDictionaryStart();
		// urgency: 0=low, 1=normal, 2=critical
		writer.WriteDictionaryEntryStart();
		writer.WriteString("urgency");
		writer.WriteVariantByte(request.Urgency switch
		{
			NotificationUrgency.Low => (byte)0,
			NotificationUrgency.Critical => (byte)2,
			_ => (byte)1,
		});
		// desktop-entry: KDE がアプリ名/アイコン/通知設定を解決するための識別子
		writer.WriteDictionaryEntryStart();
		writer.WriteString("desktop-entry");
		writer.WriteVariantString(ApplicationId);
		// 通知音はアプリ側 (PlaySoundAction 等) が担うため OS 側は無音にする
		writer.WriteDictionaryEntryStart();
		writer.WriteString("suppress-sound");
		writer.WriteVariantBool(true);
		writer.WriteDictionaryEnd(hints);

		// 重要通知は消えないように無期限、それ以外はサーバー既定
		writer.WriteInt32(request.Urgency == NotificationUrgency.Critical ? 0 : -1);
		return writer.CreateMessage();
	}

	private async Task<DBusConnection> GetConnectionAsync()
	{
		if (_connection is not null)
			return _connection;

		await _connectionLock.WaitAsync();
		try
		{
			if (_connection is null)
			{
				var address = DBusAddress.Session ?? throw new InvalidOperationException("セッションバスのアドレスを取得できません");
				var connection = new DBusConnection(address);
				await connection.ConnectAsync();
				_connection = connection;
			}
			return _connection;
		}
		finally
		{
			_connectionLock.Release();
		}
	}

	private static void FallbackNotifySend(NotificationRequest request)
	{
		try
		{
			var level = request.Urgency switch
			{
				NotificationUrgency.Low => "low",
				NotificationUrgency.Critical => "critical",
				_ => "normal",
			};
			Process.Start(new ProcessStartInfo
			{
				FileName = "notify-send",
				ArgumentList =
				{
					"-u", level,
					"-a", ApplicationName,
					$"[KEVi] {request.Title}",
					request.Message,
				},
				UseShellExecute = false,
			});
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "通知失敗");
		}
	}

	public override void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;

		_connection?.Dispose();
		_connectionLock.Dispose();
		GC.SuppressFinalize(this);
	}
}
#endif
