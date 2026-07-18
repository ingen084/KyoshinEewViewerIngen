using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Notification;
using ReactiveUI;
using Splat;
using System;
#if !WINDOWS
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
#endif

namespace KyoshinEewViewer.Desktop.Notification;

/// <summary>
/// Avalonia の <see cref="TrayIcon"/> を用いたクロスプラットフォームなトレイアイコン実装。
/// Windows/macOS/Linux (StatusNotifierItem 対応 DE) で動作する
/// </summary>
public class AvaloniaTrayIconProvider : TrayIconProvider
{
	private TrayIcon? _trayIcon;
	// 非同期の Linux 検出結果を UI スレッドから読むため volatile にする
	private volatile bool _canHideToTray;

	public override bool CanHideToTray => _trayIcon != null && _canHideToTray;

	public override void Show(TrayMenuItem[] menuItems)
	{
		if (Application.Current is not { } app)
			return;

		var menu = new NativeMenu();
		foreach (var item in menuItems)
		{
			var menuItem = new NativeMenuItem(item.Text);
			menuItem.Click += (_, _) => item.OnClicked();
			menu.Items.Add(menuItem);
		}

		_trayIcon = new TrayIcon
		{
			Icon = LoadIcon(),
			ToolTipText = NotificationProvider.ApplicationName,
			Menu = menu,
			IsVisible = true,
		};
		// macOS のメニューバーではモノクロ素材をテンプレート画像として扱わせ、ライト/ダーク外観に追従させる
		if (OperatingSystem.IsMacOS())
			MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
		// Win32・一部 Linux DE のみ発火する (macOS はメニュー経由)。クリックでメインウィンドウを開く
		_trayIcon.Clicked += (_, _) => MessageBus.Current.SendMessage(new ShowMainWindowRequested());

		TrayIcon.SetIcons(app, new TrayIcons { _trayIcon });

		// ウィンドウ格納の可否をプラットフォーム別に確定する
#if WINDOWS
		_canHideToTray = true;
#else
		if (OperatingSystem.IsMacOS())
			_canHideToTray = true;
		else if (OperatingSystem.IsLinux())
			// D-Bus 上に StatusNotifierWatcher が存在すればトレイが表示されるとみなす
			_ = DetectLinuxTrayAsync();
#endif
	}

	private static WindowIcon LoadIcon()
	{
		// macOS のメニューバーはモノクロのテンプレート画像が推奨のため専用素材を使う (他 OS はカラーのアプリアイコン)
		var name = OperatingSystem.IsMacOS() ? "tray-icon-template" : NotificationProvider.ApplicationId;
		using var asset = AssetLoader.Open(new Uri($"avares://KyoshinEewViewer.Desktop/Notification/Assets/{name}.png"));
		return new WindowIcon(asset);
	}

	public override void Dispose()
	{
		_trayIcon?.Dispose();
		_trayIcon = null;
		GC.SuppressFinalize(this);
	}

#if !WINDOWS
	/// <summary>
	/// Linux で StatusNotifierWatcher の有無を検出し、存在すればトレイ格納を有効化する。
	/// ベストエフォートで、検出できない/失敗した場合は無効のままとする (ウィンドウを見失わないため)
	/// </summary>
	private async Task DetectLinuxTrayAsync()
	{
		try
		{
			var address = DBusAddress.Session;
			if (address is null)
				return;

			using var connection = new DBusConnection(address);
			await connection.ConnectAsync();

			// org.freedesktop.DBus.NameHasOwner("org.kde.StatusNotifierWatcher")
			_canHideToTray = await connection.CallMethodAsync(
				BuildNameHasOwnerMessage(connection, "org.kde.StatusNotifierWatcher"),
				static (Message message, object? _) => message.GetBodyReader().ReadBool());
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "StatusNotifierWatcher の検出に失敗したためトレイ格納を無効化します");
		}
	}

	private static MessageBuffer BuildNameHasOwnerMessage(DBusConnection connection, string name)
	{
		using var writer = connection.GetMessageWriter();
		writer.WriteMethodCallHeader(
			destination: "org.freedesktop.DBus",
			path: "/org/freedesktop/DBus",
			@interface: "org.freedesktop.DBus",
			member: "NameHasOwner",
			signature: "s");
		writer.WriteString(name);
		return writer.CreateMessage();
	}
#endif
}
