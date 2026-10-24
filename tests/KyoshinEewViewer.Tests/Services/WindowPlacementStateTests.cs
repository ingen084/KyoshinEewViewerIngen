using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Desktop.Services;

namespace KyoshinEewViewer.Tests.Services;

public class WindowPlacementStateTests
{
	private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private static WindowPlacementScreen Screen(string id, int x)
		=> new(id, new PixelRect(x, 0, 1920, 1080), new PixelRect(x, 0, 1920, 1040));

	private static WindowPlacement Placement(WindowState state, int x, int y, WindowPlacementScreen screen, double width = 800, double height = 600)
		=> new(state, new PixelPoint(x, y), new Size(width, height), screen);

	[Fact(DisplayName = "モニタ構成の変更から抑制期間内に発生した変化は OS による移動として扱う")]
	public void ChangesWithinTheSuppressPeriodAreTreatedAsOsMoves()
	{
		var state = new WindowPlacementState();
		Assert.True(state.CanRecord(T0));

		state.OnScreensChanged(T0);
		Assert.False(state.CanRecord(T0.AddSeconds(1)));
		// Throttle で通知が遅れても、発生時刻が期間内なら記録しない
		Assert.False(state.OnPlacementChanged(T0.AddSeconds(4.9)));
		Assert.True(state.HasOsMovedWindow);
		Assert.False(state.CanRecord(T0.AddSeconds(10)));

		Assert.True(state.OnPlacementChanged(T0.AddSeconds(5)));
		Assert.False(state.HasOsMovedWindow);
		Assert.True(state.CanRecord(T0.AddSeconds(5)));
	}

	[Fact(DisplayName = "画面変更で動かなかったウィンドウは、確認した時点からユーザー操作の記録を再開する")]
	public void UnaffectedWindowResumesRecordingAfterReconcile()
	{
		var monitor = Screen("monitor-1", 0);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, 100, 100, monitor));

		// 作業領域の変更などで、ウィンドウは動かなかった
		state.OnScreensChanged(T0);
		Assert.Null(state.Reconcile(Placement(WindowState.Normal, 100, 100, monitor), [monitor], T0.AddSeconds(2)));
		Assert.False(state.HasOsMovedWindow);
		Assert.True(state.CanRecord(T0.AddSeconds(2)));

		// 直後にユーザーが全画面にした変化は記録して保存する
		Assert.True(state.OnPlacementChanged(T0.AddSeconds(3)));
		state.Record(Placement(WindowState.FullScreen, 0, 0, monitor));
		Assert.Equal(WindowState.FullScreen, state.GetSavedState(WindowState.FullScreen));
	}

	[Fact(DisplayName = "モニタの切断で OS が移動させたウィンドウは記録せず、モニタの復帰後に元の位置へ戻す")]
	public void OsMovedWindowIsRestoredAfterTheMonitorReturns()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, 2200, 120, second));

		// 2 を切断すると OS がウィンドウを 1 へ移動させる
		state.OnScreensChanged(T0);
		Assert.False(state.OnPlacementChanged(T0.AddSeconds(0.1)));
		Assert.Null(state.Reconcile(Placement(WindowState.Normal, 200, 120, first), [first], T0.AddSeconds(2)));
		Assert.True(state.HasOsMovedWindow);
		Assert.False(state.CanRecord(T0.AddSeconds(60)));
		// 戻せない間は元の位置を保存する
		Assert.Equal(new PixelPoint(2200, 120), state.GetSavedPosition([first]));

		state.OnScreensChanged(T0.AddSeconds(60));
		Assert.Equal(new PixelPoint(2200, 120), state.Reconcile(Placement(WindowState.Normal, 200, 120, first), [first, second], T0.AddSeconds(62)));
		Assert.False(state.HasOsMovedWindow);
		// 戻したことによる変化は記録しない
		Assert.False(state.OnPlacementChanged(T0.AddSeconds(62.1)));
		Assert.Equal(new PixelPoint(2200, 120), state.GetSavedPosition([first, second]));
	}

	[Fact(DisplayName = "単独で復帰して原点が変わったモニタでは、現在の座標系で戻して保存する")]
	public void MonitorReturningWithDifferentOriginUsesCurrentCoordinates()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var secondAlone = Screen("monitor-2", 0);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, 2200, 120, second));

		state.OnScreensChanged(T0);
		Assert.Equal(new PixelPoint(280, 120), state.GetSavedPosition([secondAlone]));
		Assert.Equal(new PixelPoint(280, 120), state.Reconcile(Placement(WindowState.Normal, 500, 500, secondAlone), [secondAlone], T0.AddSeconds(2)));
		Assert.Equal(new PixelPoint(280, 120), state.GetSavedPosition([secondAlone]));

		// 2 台とも復帰したら、元の座標系の位置へ戻す
		state.OnScreensChanged(T0.AddSeconds(30));
		Assert.Equal(new PixelPoint(2200, 120), state.Reconcile(Placement(WindowState.Normal, 280, 120, first), [first, second], T0.AddSeconds(32)));
	}

	[Fact(DisplayName = "最大化していたウィンドウは元のモニタで最大化し直し、保存する状態も最大化のままにする")]
	public void MaximizedWindowIsRestoredOnTheOriginalMonitor()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Maximized, 1912, -8, second));

		// OS が通常状態に戻して別のモニタへ移動させても、保存するのは意図した状態
		state.OnScreensChanged(T0);
		Assert.Equal(WindowState.Maximized, state.GetSavedState(WindowState.Normal));
		Assert.Equal(WindowState.Minimized, state.GetSavedState(WindowState.Minimized));
		Assert.Equal(new PixelPoint(1912, -8), state.Reconcile(Placement(WindowState.Normal, 100, 100, first), [first, second], T0.AddSeconds(2)));
		Assert.Equal(WindowState.Maximized, state.IntendedState);

		// 元のモニタで最大化されたままなら何もしない
		state.OnScreensChanged(T0.AddSeconds(30));
		Assert.Null(state.Reconcile(Placement(WindowState.Maximized, 1912, -8, second), [first, second], T0.AddSeconds(32)));
	}

	[Fact(DisplayName = "全画面にしても同じモニタ上の通常時の位置を保存し、別のモニタで全画面にした場合はその位置を保存する")]
	public void FullScreenKeepsTheNormalPositionOnTheSameMonitor()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, 500, 300, first));

		state.Record(Placement(WindowState.FullScreen, 0, 0, first));
		Assert.Equal(WindowState.FullScreen, state.GetSavedState(WindowState.FullScreen));
		Assert.Equal(new PixelPoint(500, 300), state.GetSavedPosition([first, second]));

		state.Record(Placement(WindowState.FullScreen, 1920, 0, second));
		Assert.Equal(new PixelPoint(1920, 0), state.GetSavedPosition([first, second]));
	}

	[Fact(DisplayName = "全画面で起動した直後は位置を保存せず、モニタ構成の変更後は元のモニタで全画面に戻す")]
	public void FullScreenAtStartupKeepsTheSavedPosition()
	{
		var first = Screen("monitor-1", 0);
		var second = Screen("monitor-2", 1920);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.FullScreen, 1920, 0, second));
		Assert.Null(state.GetSavedPosition([first, second]));

		state.OnScreensChanged(T0);
		Assert.Equal(new PixelPoint(1920, 0), state.Reconcile(Placement(WindowState.Normal, 100, 100, first), [first, second], T0.AddSeconds(2)));
		Assert.Null(state.GetSavedPosition([first, second]));
	}

	[Fact(DisplayName = "通常状態のウィンドウは端数程度のサイズの差では動かさず、OS が縮めた場合は元のサイズに戻す")]
	public void ClientSizeIsComparedWithTolerance()
	{
		var monitor = Screen("monitor-1", 0);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, 100, 100, monitor, 801.14, 600.57));

		state.OnScreensChanged(T0);
		Assert.Null(state.Reconcile(Placement(WindowState.Normal, 100, 100, monitor, 801.71, 600.0), [monitor], T0.AddSeconds(2)));

		state.OnScreensChanged(T0.AddSeconds(30));
		Assert.Equal(new PixelPoint(100, 100), state.Reconcile(Placement(WindowState.Normal, 100, 100, monitor, 640, 480), [monitor], T0.AddSeconds(32)));
		Assert.Equal(new Size(801.14, 600.57), state.IntendedClientSize);
	}

	[Fact(DisplayName = "画面外にはみ出させて置いたウィンドウは、無関係な画面変更で動かさずそのまま保存する")]
	public void WindowPlacedPartlyOffScreenIsNotMoved()
	{
		var monitor = Screen("monitor-1", 0);
		var state = new WindowPlacementState();
		state.Record(Placement(WindowState.Normal, -100, 1030, monitor));

		state.OnScreensChanged(T0);
		Assert.Equal(new PixelPoint(-100, 1030), state.GetSavedPosition([monitor]));
		Assert.Null(state.Reconcile(Placement(WindowState.Normal, -100, 1030, monitor), [monitor], T0.AddSeconds(2)));
		Assert.Equal(new PixelPoint(-100, 1030), state.GetSavedPosition([monitor]));
	}
}
