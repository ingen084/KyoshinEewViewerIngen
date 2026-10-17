using System.Diagnostics.Tracing;
using System.Text.Json;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests.Infrastructure;

internal sealed record NetworkAttempt(string Kind, string Target);

internal sealed class NetworkCase(string name)
{
	public string Name { get; } = name;
	public List<NetworkAttempt> Attempts { get; } = [];
	public Dictionary<string, int> FixtureRequests { get; } = [];

	public string[] Violations()
	{
		var errors = Attempts.Where(a => a.Kind != "HTTP").Select(a => $"{a.Kind}: {a.Target}").ToList();
		foreach (var group in Attempts.Where(a => a.Kind == "HTTP").GroupBy(a => a.Target))
		{
			var extra = group.Count() - FixtureRequests.GetValueOrDefault(group.Key);
			if (extra > 0)
				errors.Add($"未登録のHTTP要求: {group.Key} ({extra}回)");
		}
		foreach (var (target, count) in FixtureRequests)
			if (Attempts.Count(a => a.Kind == "HTTP" && a.Target == target) < count)
				errors.Add($"HTTP要求の診断イベントを取得できませんでした: {target}");
		return errors.ToArray();
	}
}

internal static class NetworkAudit
{
	private static readonly object Gate = new();
	private static readonly List<NetworkAttempt> Unassigned = [];
	private static readonly List<object> Results = [];
	private static NetworkCase? _current;
	private static NetworkListener? _listener;

	public static void Initialize()
	{
		if (AppContext.TryGetSwitch("System.Diagnostics.Tracing.EventSource.IsSupported", out var supported) && !supported)
			throw new InvalidOperationException("通信監査に必要なEventSourceが無効です。UIテストではEventSourceSupport=trueが必須です。");
		lock (Gate)
			_listener ??= new NetworkListener();
	}

	public static void Begin(string name)
	{
		NetworkIsolation.Require();
		Initialize();
		lock (Gate)
		{
			if (_current != null)
				throw new InvalidOperationException("UIテストの通信監査は並列実行できません。");
			_current = new NetworkCase(name);
		}
	}

	public static void ApproveFixture(Uri uri)
	{
		lock (Gate)
		{
			var current = _current ?? throw new InvalidOperationException("通信監査の外でテスト用HTTPが使用されました。");
			var target = Canonical(uri);
			current.FixtureRequests[target] = current.FixtureRequests.GetValueOrDefault(target) + 1;
		}
	}

	public static void Reject(string target) => Record("テスト用HTTPで拒否", target);

	public static void End()
	{
		string[] violations;
		lock (Gate)
		{
			var current = _current ?? throw new InvalidOperationException("通信監査が開始されていません。");
			_current = null;
			violations = current.Violations();
			Results.Add(new { current.Name, current.Attempts, current.FixtureRequests, violations });
			WriteReport();
		}
		if (violations.Length != 0)
			throw new XunitException("想定外の通信要求を検出しました。\n" + string.Join("\n", violations));
	}

	public static void Finish()
	{
		lock (Gate)
		{
			_listener?.Dispose();
			_listener = null;
			WriteReport();
			if (Unassigned.Count != 0)
				throw new XunitException("テスト実行外で通信要求を検出しました。\n" + string.Join("\n", Unassigned));
		}
	}

	public static string Canonical(Uri uri) => uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);

	private static void Record(string kind, string target)
	{
		lock (Gate)
			(_current?.Attempts ?? Unassigned).Add(new NetworkAttempt(kind, target));
	}

	private static void WriteReport()
	{
		var directory = Environment.GetEnvironmentVariable("KEVI_UI_TEST_AUDIT_DIR")
			?? throw new InvalidOperationException("通信監査の出力先が指定されていません。");
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "network-audit.json"), JsonSerializer.Serialize(new
		{
			network_namespace = new FileInfo("/proc/self/ns/net").LinkTarget,
			parent_network_namespace = Environment.GetEnvironmentVariable("KEVI_UI_TEST_PARENT_NETNS"),
			cases = Results,
			unassigned = Unassigned,
		}, new JsonSerializerOptions { WriteIndented = true }));
	}

	private sealed class NetworkListener : EventListener
	{
		protected override void OnEventSourceCreated(EventSource source)
		{
			if (source.Name is "System.Net.Http" or "System.Net.NameResolution" or "System.Net.Sockets")
				EnableEvents(source, EventLevel.Verbose, EventKeywords.All);
		}

		protected override void OnEventWritten(EventWrittenEventArgs e)
		{
			try { Observe(e); }
			catch (Exception ex) { Record("通信監査エラー", ex.ToString()); }
		}

		private static void Observe(EventWrittenEventArgs e)
		{
			if (e.EventSource.Name == "System.Net.Http" && e.EventName == "RequestStart")
			{
				var host = Value(e, "host");
				if (host.Contains(':') && !host.StartsWith('['))
					host = "[" + host + "]";
				var target = $"{Value(e, "scheme")}://{host}:{Value(e, "port")}{Value(e, "pathAndQuery")}";
				Record("HTTP", Uri.TryCreate(target, UriKind.Absolute, out var uri) ? Canonical(uri) : target);
			}
			else if (e.EventSource.Name == "System.Net.NameResolution" && e.EventName == "ResolutionStart")
				Record("DNS", Value(e, "hostNameOrAddress"));
			else if (e.EventSource.Name == "System.Net.Sockets" && e.EventName == "ConnectStart")
			{
				var address = Value(e, "address");
				// テスト基盤の名前付きパイプはUnixドメインソケットであり、IP通信ではない。
				if (!address.StartsWith("Unix:", StringComparison.Ordinal))
					Record("ソケット接続", address);
			}
		}

		private static string Value(EventWrittenEventArgs e, string name)
		{
			var index = e.PayloadNames?.IndexOf(name) ?? -1;
			return index >= 0 ? e.Payload?[index]?.ToString() ?? "" : "(不明)";
		}
	}
}
