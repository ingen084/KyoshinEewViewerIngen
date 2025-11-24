using KyoshinEewViewer.Services.TelegramPublishers;

namespace KyoshinEewViewer.Tests.Services.Mocks;

/// <summary>
/// テスト用のモックTelegram
/// </summary>
public class MockTelegram : Telegram
{
	private readonly byte[] _body;

	public MockTelegram(string key, string title, string rawId, DateTime arrivalTime, byte[]? body = null)
		: base(key, title, rawId, arrivalTime)
	{
		_body = body ?? [];
	}

	public override Task<Stream> GetBodyAsync()
	{
		return Task.FromResult<Stream>(new MemoryStream(_body));
	}

	public override void Cleanup()
	{
		// テスト用なので何もしない
	}
}