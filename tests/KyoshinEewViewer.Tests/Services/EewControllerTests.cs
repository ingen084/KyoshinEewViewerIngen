using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;

namespace KyoshinEewViewer.Tests.Services;

public class EewControllerTests
{
	[Fact(DisplayName = "擬似キャンセル後_同一報の再受信でキャンセル状態が解除される")]
	public void 擬似キャンセル後_同一報の再受信でキャンセル状態が解除される()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(null, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsCancelled);

		controller.Update(eew with { ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.False(actual.IsCancelled);
		Assert.False(actual.IsTrueCancelled);
	}

	[Fact(DisplayName = "確定キャンセル後_同一報の再受信ではキャンセル状態を維持する")]
	public void 確定キャンセル後_同一報の再受信ではキャンセル状態を維持する()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(eew.Id, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsTrueCancelled);

		controller.Update(eew with { ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.True(actual.IsCancelled);
		Assert.True(actual.IsTrueCancelled);
	}

	[Fact(DisplayName = "擬似キャンセル後_新しい報数の再受信でキャンセル状態が解除される")]
	public void 擬似キャンセル後_新しい報数の再受信でキャンセル状態が解除される()
	{
		var controller = CreateController();
		var updatedEews = new List<Eew[]>();
		controller.EewUpdated += (_, eews) => updatedEews.Add(eews);
		var baseTime = new DateTime(2026, 6, 18, 12, 0, 0);
		var eew = CreateEew(baseTime);

		controller.Update(eew, baseTime);
		controller.Cancelled(null, baseTime.AddSeconds(1));
		Assert.True(updatedEews.Last().Single().IsCancelled);

		controller.Update(eew with { SerialNo = 2, ReceiveTime = baseTime.AddSeconds(2) }, baseTime.AddSeconds(2));

		var actual = updatedEews.Last().Single();
		Assert.Equal(2, actual.SerialNo);
		Assert.False(actual.IsCancelled);
		Assert.False(actual.IsTrueCancelled);
	}

	private static EewController CreateController()
	{
		var config = new KyoshinEewViewerConfiguration();
		var logManager = new DefaultLogManager();
		return new EewController(
			logManager,
			null!,
			config,
			new SoundPlayerService(config, logManager),
			new WorkflowService(logManager));
	}

	private static Eew CreateEew(DateTime receiveTime)
		=> EewMock.NORMAL with
		{
			Id = "test-eew",
			DisplaySource = "強震モニタ",
			ReceiveTime = receiveTime,
			SerialNo = 1,
			IsCancelled = false,
			IsTrueCancelled = false,
			Source = EewSource.KyoshinMonitor,
		};
}
