using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using System.Net;

namespace KyoshinEewViewer.Tests.Services;

public class KyoshinMonitorOffsetAdjusterTests
{
	[Fact(DisplayName = "404 ではオフセットを延ばして同じ画像を取り直し、上限に達していれば取り直さない")]
	public async Task 上限では404の再取得を行わない()
	{
		foreach (var (initialOffset, expectedOffset, expectedFetchCount) in new[] { (4900, 5000, 2), (5000, 5000, 1) })
		{
			var timer = new KyoshinEewViewerConfiguration.TimerConfig { Offset = initialOffset, AutoOffsetIncrement = true };
			var adjuster = new KyoshinMonitorOffsetAdjuster();
			var fetchCount = 0;

			using var response = await adjuster.FetchAsync(timer, () =>
			{
				fetchCount++;
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
			});

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
			Assert.Equal(expectedFetchCount, fetchCount);
			Assert.Equal(expectedOffset, timer.Offset);
		}
	}
}
