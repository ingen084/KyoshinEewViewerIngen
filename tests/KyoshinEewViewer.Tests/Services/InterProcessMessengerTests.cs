using KyoshinEewViewer.Desktop.Services.InterProcess;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Tests.Services;

public class InterProcessMessengerTests
{
	private static readonly InterProcessMessageType<EchoRequest, EchoResponse> Echo
		= new("Echo", InterProcessTestJsonContext.Default.EchoRequest, InterProcessTestJsonContext.Default.EchoResponse);
	private static readonly InterProcessMessageType<EchoRequest, EchoResponse> Fail
		= new("Fail", InterProcessTestJsonContext.Default.EchoRequest, InterProcessTestJsonContext.Default.EchoResponse);
	private static readonly InterProcessMessageType<EchoRequest, EchoResponse> Unregistered
		= new("Unregistered", InterProcessTestJsonContext.Default.EchoRequest, InterProcessTestJsonContext.Default.EchoResponse);

	private static string CreateEndpointName()
		=> InterProcessMessenger.CreateEndpointName($"test-{Guid.NewGuid().ToString("N")[..8]}");

	private static InterProcessMessenger StartMessenger()
	{
		var messenger = new InterProcessMessenger(CreateEndpointName(), NullLogger.Instance);
		messenger.Register(Echo, (request, _) => Task.FromResult(new EchoResponse($"{request.Text}!", request.Number * 2)));
		messenger.Register(Fail, (_, _) => Task.FromException<EchoResponse>(new InvalidOperationException("処理できません")));
		messenger.Start();
		return messenger;
	}

	[Fact(DisplayName = "要求を送ると登録した処理の応答が返り、同時に複数送っても応答を取り違えない")]
	public async Task SendAsync_ReturnsHandlerResponse()
	{
		using var messenger = StartMessenger();

		var single = await InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest("地震", 1));
		Assert.Equal(new EchoResponse("地震!", 2), single);

		// NamedPipeClientStream.ConnectAsync は接続を待つ間スレッドプールのスレッドを塞ぐので、コア数の少ないマシンでも詰まらない数にする
		var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
			InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest($"#{i}", i))));
		for (var i = 0; i < responses.Length; i++)
			Assert.Equal(new EchoResponse($"#{i}!", i * 2), responses[i]);
	}

	[Fact(DisplayName = "相手が処理できない要求は送り元で InterProcessException になり、その後の要求には影響しない")]
	public async Task SendAsync_ThrowsWhenRemoteCannotHandle()
	{
		using var messenger = StartMessenger();

		var unregistered = await Assert.ThrowsAsync<InterProcessException>(() =>
			InterProcessMessenger.SendAsync(messenger.EndpointName, Unregistered, new EchoRequest("a", 0)));
		Assert.Contains("Unregistered", unregistered.Message);

		var failed = await Assert.ThrowsAsync<InterProcessException>(() =>
			InterProcessMessenger.SendAsync(messenger.EndpointName, Fail, new EchoRequest("a", 0)));
		Assert.Contains("処理できません", failed.Message);

		Assert.Equal(new EchoResponse("b!", 2), await InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest("b", 1)));
	}

	[Fact(DisplayName = "要求を送らずに切断した接続があっても、その後の要求に応答する")]
	public async Task Server_KeepsRespondingAfterEmptyConnection()
	{
		using var messenger = StartMessenger();

		for (var i = 0; i < 3; i++)
		{
			await using var client = new NamedPipeClientStream(".", messenger.EndpointName, PipeDirection.InOut);
			await client.ConnectAsync(1000);
		}

		Assert.Equal(new EchoResponse("a!", 2), await InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest("a", 1)));
	}

	[Fact(DisplayName = "待ち受けていない接続先や、待ち受けを終了した接続先へ送ると InterProcessException になる")]
	public async Task SendAsync_ThrowsWhenNotListening()
	{
		await Assert.ThrowsAsync<InterProcessException>(() =>
			InterProcessMessenger.SendAsync(CreateEndpointName(), Echo, new EchoRequest("a", 0)));

		var messenger = StartMessenger();
		Assert.Equal(new EchoResponse("a!", 0), await InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest("a", 0)));
		messenger.Dispose();
		await Assert.ThrowsAsync<InterProcessException>(() =>
			InterProcessMessenger.SendAsync(messenger.EndpointName, Echo, new EchoRequest("a", 0)));
	}
}

public sealed record EchoRequest(string Text, int Number);

public sealed record EchoResponse(string Text, int Number);

[JsonSerializable(typeof(EchoRequest))]
[JsonSerializable(typeof(EchoResponse))]
internal partial class InterProcessTestJsonContext : JsonSerializerContext;
