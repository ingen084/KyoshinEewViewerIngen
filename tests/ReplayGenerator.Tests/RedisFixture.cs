using StackExchange.Redis;
using Testcontainers.Redis;

namespace ReplayGenerator.Tests;

[CollectionDefinition("Redis")]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>;

public sealed class RedisFixture : IAsyncLifetime
{
	private RedisContainer? _container;

	public IConnectionMultiplexer Multiplexer { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		_container = new RedisBuilder("redis:7-alpine").Build();
		await _container.StartAsync();
		Multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
	}

	public async Task DisposeAsync()
	{
		await Multiplexer.DisposeAsync();
		if (_container != null)
			await _container.DisposeAsync();
	}

	public async Task FlushAsync()
	{
		await Multiplexer.GetDatabase().ExecuteAsync("FLUSHDB");
	}
}
