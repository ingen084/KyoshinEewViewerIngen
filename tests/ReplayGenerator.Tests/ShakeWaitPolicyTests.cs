using ReplayGenerator.Services;

namespace ReplayGenerator.Tests;

public class ShakeWaitPolicyTests
{
	[Fact(DisplayName = "スナップショットが null のとき待機は30秒")]
	public void NullSnapshot_Returns30()
	{
		Assert.Equal(30, ShakeWaitPolicy.DetermineWaitSeconds(null));
	}

	[Fact(DisplayName = "eews が無い JSON は30秒")]
	public void MissingEews_Returns30()
	{
		Assert.Equal(30, ShakeWaitPolicy.DetermineWaitSeconds("""{"revision":0}"""));
	}

	[Fact(DisplayName = "深さ150km以下かつM5以下なら60秒")]
	public void ShallowAndLowMagnitude_Returns60()
	{
		var json = """
			{"eews":[{"hypocenter":{"depth":100},"magnitude":4.5}]}
			""";
		Assert.Equal(60, ShakeWaitPolicy.DetermineWaitSeconds(json));
	}

	[Fact(DisplayName = "深さが深い場合は180秒")]
	public void DeepHypocenter_Returns180()
	{
		var json = """
			{"eews":[{"hypocenter":{"depth":200},"magnitude":4}]}
			""";
		Assert.Equal(180, ShakeWaitPolicy.DetermineWaitSeconds(json));
	}

	[Fact(DisplayName = "Mが大きい場合は180秒")]
	public void LargeMagnitude_Returns180()
	{
		var json = """
			{"eews":[{"hypocenter":{"depth":100},"magnitude":6}]}
			""";
		Assert.Equal(180, ShakeWaitPolicy.DetermineWaitSeconds(json));
	}

	[Fact(DisplayName = "不正JSONは30秒")]
	public void InvalidJson_Returns30()
	{
		Assert.Equal(30, ShakeWaitPolicy.DetermineWaitSeconds("{not json"));
	}
}
