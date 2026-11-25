using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

namespace KyoshinEewViewer.Tests.Services;

public class EewTriggerTests
{
	[Fact(DisplayName = "訓練の緊急地震速報は「訓練も対象にする」が有効なトリガーでのみ発火し、訓練以外は設定によらず発火する")]
	public void 訓練は訓練も対象にするトリガーでのみ発火する()
	{
		var training = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with { Source = EewSource.DmdataEewClient, IsTraining = true }, false);
		var real = EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL with { Source = EewSource.DmdataEewClient }, false);
		Assert.True(training.IsTraining);
		Assert.True(training.IsExternalRelayProhibited);

		Assert.False(new EewTrigger().CheckTrigger(training));
		Assert.True(new EewTrigger { IncludeTraining = true }.CheckTrigger(training));
		Assert.True(new EewTrigger().CheckTrigger(real));
		Assert.True(new EewTrigger { IncludeTraining = true }.CheckTrigger(real));
	}

	private static EewEvent CreateEewEvent(EewEventType type, string id, float magnitude, bool isTraining = false)
		=> EewEvent.FromEewModel(null!, type, EewMock.NORMAL with
		{
			Id = id,
			IsTraining = isTraining,
			Hypocenter = EewMock.NORMAL.Hypocenter! with { Magnitude = magnitude },
		}, false);

	[Fact(DisplayName = "重複制御が無効な場合は同じEEWの続報でも条件を満たすたびにトリガーされる")]
	public void 重複制御が無効な場合は同じEEWの続報でもトリガーされる()
	{
		var trigger = new EewTrigger { Magnitude = 6.0f };

		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 6.0f)));
		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.1f)));
		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.Final, "20250101000000", 6.2f)));
	}

	[Fact(DisplayName = "重複制御が有効な場合は同じEEWで条件を満たした最初の1回だけトリガーされる")]
	public void 重複制御が有効な場合は同じEEWで最初の1回だけトリガーされる()
	{
		var trigger = new EewTrigger { Magnitude = 6.0f, OncePerEew = true };

		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 6.0f)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.1f)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.Final, "20250101000000", 6.2f)));
	}

	[Fact(DisplayName = "重複制御が有効でも条件を満たさないイベントでは消費されず、最初に条件を満たした続報でトリガーされる")]
	public void 条件を満たさないイベントでは重複制御が消費されない()
	{
		var trigger = new EewTrigger { Magnitude = 6.0f, OncePerEew = true };

		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 5.9f)));
		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.1f)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.2f)));
	}

	[Fact(DisplayName = "重複制御はEEWごとに判定され、別のEEWでは再びトリガーされる")]
	public void 重複制御は別のEEWでは再びトリガーされる()
	{
		var trigger = new EewTrigger { Magnitude = 6.0f, OncePerEew = true };

		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 6.0f)));
		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000100", 6.0f)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.5f)));
	}

	[Fact(DisplayName = "重複制御が有効な場合、訓練と実際のEEWは別々に数える")]
	public void 訓練と実際のEEWは別々に数える()
	{
		var trigger = new EewTrigger { Magnitude = 6.0f, OncePerEew = true, IncludeTraining = true };

		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 6.0f, isTraining: true)));
		Assert.True(trigger.CheckTrigger(CreateEewEvent(EewEventType.New, "20250101000000", 6.0f)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.1f, isTraining: true)));
		Assert.False(trigger.CheckTrigger(CreateEewEvent(EewEventType.UpdateNewSerial, "20250101000000", 6.1f)));
	}
}
