using System.Net.NetworkInformation;

namespace KyoshinEewViewer.UiTests.Infrastructure;

internal static class NetworkIsolation
{
	public static void Require()
	{
		// 環境変数だけを信用せず、実際の名前空間とインターフェースを検査する。
		if (!OperatingSystem.IsLinux())
			throw new InvalidOperationException("UIテストは通信を隔離したLinux環境で実行してください。tests/UiTests/run.ps1 または run.sh を使用します。");

		var parent = Environment.GetEnvironmentVariable("KEVI_UI_TEST_PARENT_NETNS");
		var current = new FileInfo("/proc/self/ns/net").LinkTarget;
		if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(current) || parent == current)
			throw new InvalidOperationException("UIテストのネットワーク名前空間が分離されていません。");

		if (NetworkInterface.GetAllNetworkInterfaces().Any(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
			throw new InvalidOperationException("UIテスト環境にループバック以外のネットワークが存在します。");

		if (File.ReadAllText("/etc/nsswitch.conf").Trim() != "hosts: files")
			throw new InvalidOperationException("UIテストの名前解決がローカルファイルに限定されていません。");

		var home = Environment.GetEnvironmentVariable("KEVI_UI_TEST_HOME");
		if (string.IsNullOrEmpty(home) || Path.GetFullPath(home) != Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
			throw new InvalidOperationException("UIテストの設定保存先が通常のユーザープロファイルから分離されていません。");
	}
}
