using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Desktop.Services.InterProcess;

/// <summary>
/// インスタンス間でやりとりするメッセージの種類。要求と応答の型を JSON の型情報とともに決める
/// </summary>
/// <param name="Name">メッセージを識別する名前</param>
public sealed record InterProcessMessageType<TRequest, TResponse>(
	string Name,
	JsonTypeInfo<TRequest> RequestTypeInfo,
	JsonTypeInfo<TResponse> ResponseTypeInfo);

/// <summary>
/// インスタンス間通信に失敗した (接続できない・応答がない・相手が処理できなかった)
/// </summary>
public sealed class InterProcessException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// 同じユーザーで動いている KEVi のインスタンス間で、要求と応答をやりとりする。
/// <para>
/// 各インスタンスは自分のプロセス ID の接続先で待ち受ける。1回の接続で、要求を1件受け取って応答を1件返す。
/// データは「4バイトの長さ (リトルエンディアン) + UTF-8 の JSON」で送る。
/// </para>
/// <para>
/// 処理するメッセージは <see cref="Register{TRequest, TResponse}"/> で登録する。このクラスは個々のメッセージの意味を知らない。
/// </para>
/// </summary>
public sealed class InterProcessMessenger : IDisposable
{
	private const string EndpointPrefix = "KyoshinEewViewerIngen.";
	private const int MaxFrameLength = 1024 * 1024;
	private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

	private readonly ConcurrentDictionary<string, Func<JsonElement, CancellationToken, Task<JsonElement>>> _handlers = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private ILogger Logger { get; }

	/// <summary>待ち受ける接続先</summary>
	public string EndpointName { get; }

	public InterProcessMessenger(string endpointName, ILogger logger)
	{
		EndpointName = endpointName;
		Logger = logger;
	}

	/// <summary>
	/// 指定したプロセスの接続先の名前を返す
	/// </summary>
	public static string GetEndpointName(int processId) => CreateEndpointName(processId.ToString());

	internal static string CreateEndpointName(string id)
	{
		var name = EndpointPrefix + id;
		// Linux/macOS では Unix ドメインソケットのファイルになる。前回同じ名前で異常終了したときのファイルを消せるよう、場所を自分で決める
		return OperatingSystem.IsWindows() ? name : Path.Combine(Path.GetTempPath(), name + ".sock");
	}

	/// <summary>
	/// メッセージを処理する関数を登録する。同じ種類を登録し直すと置き換わる
	/// </summary>
	public void Register<TRequest, TResponse>(
		InterProcessMessageType<TRequest, TResponse> type,
		Func<TRequest, CancellationToken, Task<TResponse>> handler)
	{
		_handlers[type.Name] = async (payload, cancellationToken) =>
		{
			var request = payload.Deserialize(type.RequestTypeInfo)
				?? throw new InvalidDataException($"要求の内容を読み取れませんでした: {type.Name}");
			var response = await handler(request, cancellationToken);
			return JsonSerializer.SerializeToElement(response, type.ResponseTypeInfo);
		};
	}

	/// <summary>
	/// 待ち受けを開始する
	/// </summary>
	public void Start()
	{
		if (!OperatingSystem.IsWindows() && File.Exists(EndpointName))
		{
			try
			{
				File.Delete(EndpointName);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "前回のソケットファイルを削除できませんでした: {Endpoint}", EndpointName);
			}
		}
		Logger.LogDebug("インスタンス間通信の待ち受けを開始します: {Endpoint}", EndpointName);
		_ = Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
	}

	private NamedPipeServerStream CreateServerStream()
		=> new(EndpointName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
			PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

	private async Task RunServerAsync(CancellationToken cancellationToken)
	{
		NamedPipeServerStream? waiting = null;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					waiting ??= CreateServerStream();
					await waiting.WaitForConnectionAsync(cancellationToken);
					var connected = waiting;
					waiting = null;
					try
					{
						// Linux/macOS では待ち受けがすべて閉じるとソケットも閉じるので、応答する前に次の待ち受けを作っておく
						waiting = CreateServerStream();
					}
					finally
					{
						_ = Task.Run(() => HandleConnectionAsync(connected, cancellationToken), CancellationToken.None);
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "インスタンス間通信の待ち受けに失敗しました: {Endpoint}", EndpointName);
					waiting?.Dispose();
					waiting = null;
					try
					{
						await Task.Delay(RetryDelay, cancellationToken);
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}
		}
		finally
		{
			waiting?.Dispose();
		}
	}

	private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
	{
		await using (pipe)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(ExchangeTimeout);
			try
			{
				var request = await ReadFrameAsync(pipe, InterProcessEnvelopeJsonContext.Default.InterProcessRequestEnvelope, cts.Token);
				var response = await DispatchAsync(request, cts.Token);
				await WriteFrameAsync(pipe, response, InterProcessEnvelopeJsonContext.Default.InterProcessResponseEnvelope, cts.Token);
				// 相手が応答を読み終えて切断するまで待つ。先に閉じると、読まれる前の応答が捨てられることがある
				var buffer = new byte[1];
				while (await pipe.ReadAsync(buffer, cts.Token) > 0)
				{
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (EndOfStreamException)
			{
				// Windows では接続先の存在を確かめるだけでも接続され、何も送らずに切断される
				Logger.LogDebug("要求を受け取る前に切断されました: {Endpoint}", EndpointName);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "インスタンス間通信の要求に応答できませんでした: {Endpoint}", EndpointName);
			}
		}
	}

	private async Task<InterProcessResponseEnvelope> DispatchAsync(InterProcessRequestEnvelope request, CancellationToken cancellationToken)
	{
		if (!_handlers.TryGetValue(request.Type, out var handler))
			return new(false, null, $"対応していない要求です: {request.Type}");
		try
		{
			return new(true, await handler(request.Payload, cancellationToken), null);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogWarning(ex, "インスタンス間通信の要求の処理に失敗しました: {Type}", request.Type);
			return new(false, null, ex.Message);
		}
	}

	/// <summary>
	/// 指定した接続先へ要求を送り、応答を受け取る
	/// </summary>
	/// <exception cref="InterProcessException">接続できない、応答がない、または相手が処理できなかった</exception>
	public static async Task<TResponse> SendAsync<TRequest, TResponse>(
		string endpointName,
		InterProcessMessageType<TRequest, TResponse> type,
		TRequest request,
		CancellationToken cancellationToken = default)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(ConnectTimeout + ExchangeTimeout);
		InterProcessResponseEnvelope response;
		try
		{
			await using var pipe = new NamedPipeClientStream(".", endpointName, PipeDirection.InOut,
				PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
			await pipe.ConnectAsync(ConnectTimeout, cts.Token);
			var envelope = new InterProcessRequestEnvelope(type.Name, JsonSerializer.SerializeToElement(request, type.RequestTypeInfo));
			await WriteFrameAsync(pipe, envelope, InterProcessEnvelopeJsonContext.Default.InterProcessRequestEnvelope, cts.Token);
			response = await ReadFrameAsync(pipe, InterProcessEnvelopeJsonContext.Default.InterProcessResponseEnvelope, cts.Token);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new InterProcessException($"{endpointName} と通信できませんでした ({type.Name})", ex);
		}

		if (!response.Ok)
			throw new InterProcessException($"{endpointName} が要求を処理できませんでした ({type.Name}): {response.Error}");
		try
		{
			if (response.Payload is { } payload && payload.Deserialize(type.ResponseTypeInfo) is { } result)
				return result;
		}
		catch (JsonException ex)
		{
			throw new InterProcessException($"{endpointName} の応答を読み取れませんでした ({type.Name})", ex);
		}
		throw new InterProcessException($"{endpointName} の応答が空でした ({type.Name})");
	}

	private static async Task WriteFrameAsync<T>(Stream stream, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
	{
		var body = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
		if (body.Length > MaxFrameLength)
			throw new InvalidDataException($"送るデータが大きすぎます: {body.Length} バイト");
		var frame = new byte[4 + body.Length];
		BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
		body.CopyTo(frame, 4);
		await stream.WriteAsync(frame, cancellationToken);
		await stream.FlushAsync(cancellationToken);
	}

	private static async Task<T> ReadFrameAsync<T>(Stream stream, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
	{
		var header = new byte[4];
		await stream.ReadExactlyAsync(header, cancellationToken);
		var length = BinaryPrimitives.ReadInt32LittleEndian(header);
		if (length <= 0 || length > MaxFrameLength)
			throw new InvalidDataException($"受け取ったデータの長さが不正です: {length}");
		var body = new byte[length];
		await stream.ReadExactlyAsync(body, cancellationToken);
		return JsonSerializer.Deserialize(body, typeInfo)
			?? throw new InvalidDataException("受け取ったデータを読み取れませんでした");
	}

	public void Dispose()
	{
		// 応答中の接続がトークンを参照し続けるので、破棄はせず取り消すだけにする
		_cancellationTokenSource.Cancel();
		if (!OperatingSystem.IsWindows())
		{
			try
			{
				File.Delete(EndpointName);
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex, "ソケットファイルを削除できませんでした: {Endpoint}", EndpointName);
			}
		}
	}
}

internal sealed record InterProcessRequestEnvelope(string Type, JsonElement Payload);

internal sealed record InterProcessResponseEnvelope(bool Ok, JsonElement? Payload, string? Error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InterProcessRequestEnvelope))]
[JsonSerializable(typeof(InterProcessResponseEnvelope))]
internal partial class InterProcessEnvelopeJsonContext : JsonSerializerContext;
