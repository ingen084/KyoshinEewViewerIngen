using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataReconnectionStrategyのテスト
/// </summary>
public class DmdataReconnectionStrategyTests : IDisposable
{
	private readonly DmdataReconnectionStrategy _strategy;

	public DmdataReconnectionStrategyTests()
	{
		_strategy = new DmdataReconnectionStrategy();
	}

	[Fact(DisplayName = "初期状態_正しい値を持つ")]
	public void 初期状態_正しい値を持つ()
	{
		// Assert
		Assert.Equal(10, _strategy.ReconnectBackoffTime);
		Assert.Equal(0, _strategy.TemporaryFailureCount);
		Assert.Null(_strategy.LastTemporaryFailureTime);
	}

	[Fact(DisplayName = "RecordTemporaryFailure_失敗回数が増加する")]
	public void RecordTemporaryFailure_失敗回数が増加する()
	{
		// Act
		_strategy.RecordTemporaryFailure();

		// Assert
		Assert.Equal(1, _strategy.TemporaryFailureCount);
		Assert.NotNull(_strategy.LastTemporaryFailureTime);
	}

	[Fact(DisplayName = "RecordTemporaryFailure_複数回呼び出し_失敗回数が累積する")]
	public void RecordTemporaryFailure_複数回呼び出し_失敗回数が累積する()
	{
		// Act
		_strategy.RecordTemporaryFailure();
		_strategy.RecordTemporaryFailure();
		_strategy.RecordTemporaryFailure();

		// Assert
		Assert.Equal(3, _strategy.TemporaryFailureCount);
	}

	[Fact(DisplayName = "ResetTemporaryFailure_状態がリセットされる")]
	public void ResetTemporaryFailure_状態がリセットされる()
	{
		// Arrange
		_strategy.RecordTemporaryFailure();
		_strategy.RecordTemporaryFailure();

		// Act
		_strategy.ResetTemporaryFailure();

		// Assert
		Assert.Equal(0, _strategy.TemporaryFailureCount);
		Assert.Null(_strategy.LastTemporaryFailureTime);
	}

	[Fact(DisplayName = "ResetBackoffTime_バックオフ時間がリセットされる")]
	public void ResetBackoffTime_バックオフ時間がリセットされる()
	{
		// Arrange
		_strategy.RecordTemporaryFailure();
		var originalBackoff = _strategy.ReconnectBackoffTime;

		// Act
		_strategy.ResetBackoffTime();

		// Assert
		Assert.Equal(10, _strategy.ReconnectBackoffTime);
	}

	[Fact(DisplayName = "ReconnectImmediately_バックオフ時間が5秒に設定される")]
	public void ReconnectImmediately_バックオフ時間が5秒に設定される()
	{
		// Act
		_strategy.ReconnectImmediately();

		// Assert
		Assert.Equal(5, _strategy.ReconnectBackoffTime);
	}

	[Fact(DisplayName = "WebSocketReconnectTimer_初期化できる")]
	public void WebSocketReconnectTimer_初期化できる()
	{
		// Act
		_strategy.InitializeWebSocketReconnectTimer(
			() => true,
			() => { }
		);

		// Assert - タイマーが初期化されたことを確認（例外が発生しない）
		Assert.NotNull(_strategy);
	}

	[Fact(DisplayName = "TemporaryFailureRecoveryTimer_初期化できる")]
	public void TemporaryFailureRecoveryTimer_初期化できる()
	{
		// Act
		_strategy.InitializeTemporaryFailureRecoveryTimer(() => { });

		// Assert - タイマーが初期化されたことを確認（例外が発生しない）
		Assert.NotNull(_strategy);
	}

	[Fact(DisplayName = "StartWebSocketReconnectTimer_例外が発生しない")]
	public void StartWebSocketReconnectTimer_例外が発生しない()
	{
		// Arrange
		_strategy.InitializeWebSocketReconnectTimer(
			() => false,
			() => { }
		);

		// Act & Assert - 例外が発生しないことを確認
		var exception = Record.Exception(() => _strategy.StartWebSocketReconnectTimer());
		Assert.Null(exception);
	}

	[Fact(DisplayName = "StopWebSocketReconnectTimer_例外が発生しない")]
	public void StopWebSocketReconnectTimer_例外が発生しない()
	{
		// Arrange
		_strategy.InitializeWebSocketReconnectTimer(
			() => false,
			() => { }
		);

		// Act & Assert - 例外が発生しないことを確認
		var exception = Record.Exception(() => _strategy.StopWebSocketReconnectTimer());
		Assert.Null(exception);
	}

	[Fact(DisplayName = "StopTemporaryFailureRecoveryTimer_例外が発生しない")]
	public void StopTemporaryFailureRecoveryTimer_例外が発生しない()
	{
		// Arrange
		_strategy.InitializeTemporaryFailureRecoveryTimer(() => { });

		// Act & Assert - 例外が発生しないことを確認
		var exception = Record.Exception(() => _strategy.StopTemporaryFailureRecoveryTimer());
		Assert.Null(exception);
	}

	[Fact(DisplayName = "Dispose_例外が発生しない")]
	public void Dispose_例外が発生しない()
	{
		// Arrange
		_strategy.InitializeWebSocketReconnectTimer(() => false, () => { });
		_strategy.InitializeTemporaryFailureRecoveryTimer(() => { });

		// Act & Assert - 例外が発生しないことを確認
		var exception = Record.Exception(() => _strategy.Dispose());
		Assert.Null(exception);
	}

	public void Dispose()
	{
		_strategy?.Dispose();
		GC.SuppressFinalize(this);
	}
}
