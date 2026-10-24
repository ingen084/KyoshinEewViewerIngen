using Avalonia;
using KyoshinEewViewer.Desktop.Services;

namespace KyoshinEewViewer.Tests.Services;

public class WindowPlacementScreenTests
{
	private static WindowPlacementScreen Screen(string? id, int x, int y = 0, int width = 1920, int height = 1080)
		=> new(id, new PixelRect(x, y, width, height), new PixelRect(x, y, width, height - 40));

	[Fact(DisplayName = "2台とも切断した後に2台目だけが復帰しても、元のモニタ内の位置を維持する")]
	public void SecondMonitorKeepsRelativePositionAfterBothMonitorsAreTurnedOff()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var position = new PixelPoint(2200, 120);

		// 1 → 2 の順で切断すると、2 の原点は 1920 → 0 に移動し、最後に全画面が消える。
		var secondAlone = Screen("monitor-2", 0);
		Assert.Null(first.FindCurrentScreen([secondAlone]));
		Assert.Equal(new PixelPoint(280, 120), second.TranslatePosition(position, second.FindCurrentScreen([secondAlone])!));
		Assert.Null(second.FindCurrentScreen([]));

		// 2 だけの復帰後も、2 台とも復帰した後も、元の画面内の位置を維持する。
		var reconnected = Screen("monitor-2", 0);
		Assert.Equal(new PixelPoint(280, 120), second.TranslatePosition(position, second.FindCurrentScreen([reconnected])!));
		Assert.Equal(position, second.TranslatePosition(position, second.FindCurrentScreen([first, second])!));
	}

	[Fact(DisplayName = "モニタは識別子で照合し、保存した座標を別のモニタが占めていても復元先にしない")]
	public void FindCurrentScreenMatchesByIdentity()
	{
		// 保存した座標に別のモニタがあっても、そのモニタは選ばない。
		Assert.Null(Screen("monitor-1", 0).FindCurrentScreen([Screen("monitor-2", 0)]));

		// モニタの製品名や列挙順ではなく、デバイスごとの ID を使用する。
		var reconnected = Screen("device-path-2", 0);
		Assert.Same(reconnected, Screen("device-path-2", 1920).FindCurrentScreen([reconnected, Screen("device-path-1", 1920)]));

		// 同じ識別子が複数ある場合は、どれかを勝手に選ばない。
		Assert.Null(Screen("same-name", 1920).FindCurrentScreen([Screen("same-name", 0), Screen("same-name", 1920)]));
	}

	[Fact(DisplayName = "識別子が取れないモニタ同士は位置で照合し、識別子の有無が異なるモニタとは照合しない")]
	public void ScreensWithoutIdentityAreMatchedByBounds()
	{
		var unknown = Screen(null, 1920);
		Assert.Same(unknown, Screen(null, 1920).FindCurrentScreen([Screen(null, 0), unknown]));
		Assert.Null(Screen(null, 1920).FindCurrentScreen([Screen("monitor-2", 1920)]));
		Assert.Null(Screen("monitor-2", 1920).FindCurrentScreen([Screen(null, 1920)]));
	}

	[Theory(DisplayName = "負の座標や上下に並んだモニタでも原点の移動に合わせて位置を換算する")]
	[InlineData(-1920, 0, -1640, 120, 0, 0, 280, 120)]
	[InlineData(0, -1080, 280, -960, 0, 0, 280, 120)]
	[InlineData(1920, 0, 2200, 120, -1920, -1080, -1640, -960)]
	public void TranslatePositionHandlesNegativeAndVerticalMonitorCoordinates(
		int oldX, int oldY, int x, int y, int newX, int newY, int expectedX, int expectedY)
	{
		var saved = Screen("monitor", oldX, oldY);
		var current = Screen("monitor", newX, newY);
		Assert.Equal(new PixelPoint(expectedX, expectedY), saved.TranslatePosition(new PixelPoint(x, y), current));
	}

	[Fact(DisplayName = "配置と解像度が同じモニタでは位置を変えない")]
	public void IdenticalScreenLayoutDoesNotChangePosition()
	{
		var screen = Screen("monitor", 1920);
		var position = new PixelPoint(2250, 200);
		Assert.Equal(position, screen.TranslatePosition(position, screen));
	}

	[Fact(DisplayName = "解像度が変わった場合はモニタ内の相対的な位置を保つ")]
	public void TranslatePositionKeepsRelativePositionWhenResolutionChanges()
	{
		var saved = Screen("monitor", 1920, width: 3840, height: 2160);
		var current = Screen("monitor", 0);
		Assert.Equal(new PixelPoint(500, 300), saved.TranslatePosition(new PixelPoint(2920, 600), current));
	}

	[Fact(DisplayName = "作業領域が縮んでタイトルバーが隠れる場合は、画面内に収める")]
	public void SmallerWorkingAreaKeepsTheTitleBarVisible()
	{
		var saved = Screen("monitor", 1920);
		var current = new WindowPlacementScreen("monitor", new PixelRect(0, 0, 800, 600), new PixelRect(0, 30, 800, 530));
		var translated = saved.TranslatePosition(new PixelPoint(3720, 1000), current);
		Assert.Equal(new PixelPoint(750, 520), translated);
		Assert.True(WindowPlacementScreen.ContainsTitleBar(current.WorkingArea, translated));
	}

	[Fact(DisplayName = "元から作業領域の外に置かれたウィンドウは、同じモニタでは動かさない")]
	public void WindowOutsideTheWorkingAreaIsNotMovedOnTheSameScreen()
	{
		var screen = Screen("monitor", 0);
		// 左端からはみ出させた位置と、タイトルバーがタスクバーに重なる位置
		foreach (var position in new[] { new PixelPoint(-100, 200), new PixelPoint(300, 1030) })
			Assert.Equal(position, screen.TranslatePosition(position, screen));
	}
}
