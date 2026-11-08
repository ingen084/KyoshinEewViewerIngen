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
}
