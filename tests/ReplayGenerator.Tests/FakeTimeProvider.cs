namespace ReplayGenerator.Tests;

public sealed class FakeTimeProvider : TimeProvider
{
	private DateTimeOffset _utcNow = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset GetUtcNow() => _utcNow;

	public void SetUtcNow(DateTimeOffset utc) => _utcNow = utc;

	public void Advance(TimeSpan delta) => _utcNow += delta;
}
