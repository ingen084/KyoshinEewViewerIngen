using KyoshinMonitorLib;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;

namespace ReplayGenerator.Tests;

public class EqMonitorEventMessageConverterTests
{
	private static readonly DateTime ReplayTime = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

	[Fact(DisplayName = "通常の EventMessage から Eew に変換できる")]
	public void NormalMessage_ToEew_MapsFields()
	{
		var msg = new EqMonitorEventMessage
		{
			EventId = "evt-normal",
			Type = "EEW",
			SerialNo = 3,
			OriginTime = "2026-03-15T11:59:30.000Z",
			MaxIntensity = "5+",
			Magnitude = 5.2,
			IsWarning = true,
			IsLastInfo = true,
			Hypocenter = new EqMonitorHypocenter
			{
				Latitude = 35.1,
				Longitude = 139.2,
				Depth = 10,
				Name = "テスト海域",
			},
			Regions =
			[
				new EqMonitorRegion { Code = "131", Name = "東京", Intensity = "4" },
			],
		};

		var eew = EqMonitorEventMessageConverter.ToEew(msg, ReplayTime);

		Assert.NotNull(eew);
		Assert.Equal("evt-normal", eew!.Id);
		Assert.Equal(3, eew.SerialNo);
		Assert.True(eew.IsFinal);
		Assert.True(eew.IsWarning);
		Assert.Equal(JmaIntensity.Int5Upper, eew.MaxIntensity);
		Assert.NotNull(eew.Hypocenter);
		Assert.Equal(5.2f, eew.Hypocenter!.Magnitude);
		Assert.Equal(10, eew.Hypocenter.Depth);
		Assert.NotNull(eew.IntensityForecastMap);
		Assert.Equal(JmaIntensity.Int4, eew.IntensityForecastMap![131]);
	}

	[Fact(DisplayName = "キャンセル報は IsCancelled を立てる")]
	public void CancelMessage_ToEew_SetsCancelled()
	{
		var msg = new EqMonitorEventMessage
		{
			EventId = "evt-cancel",
			SerialNo = 1,
			IsCancel = true,
			IsWarning = false,
		};

		var eew = EqMonitorEventMessageConverter.ToEew(msg, ReplayTime);

		Assert.NotNull(eew);
		Assert.True(eew!.IsCancelled);
		Assert.True(eew.IsTrueCancelled);
		Assert.Null(eew.Hypocenter);
	}

	[Fact(DisplayName = "originTime も arrivalTime も無いときは replayTime を発生時刻に使う")]
	public void NoTimeFields_UsesReplayTime()
	{
		var msg = new EqMonitorEventMessage
		{
			EventId = "evt-notime",
			SerialNo = 0,
			Hypocenter = new EqMonitorHypocenter
			{
				Latitude = 0,
				Longitude = 0,
				Depth = 0,
			},
		};

		var eew = EqMonitorEventMessageConverter.ToEew(msg, ReplayTime);

		Assert.NotNull(eew);
		Assert.Equal(ReplayTime, eew!.Hypocenter!.OccurrenceTime);
	}

	[Fact(DisplayName = "不完全なメッセージでも例外にならずに変換できる")]
	public void MinimalMessage_DoesNotThrow()
	{
		var msg = new EqMonitorEventMessage
		{
			EventId = "",
			SerialNo = 0,
		};

		var eew = EqMonitorEventMessageConverter.ToEew(msg, ReplayTime);

		Assert.NotNull(eew);
		Assert.Equal("", eew!.Id);
	}
}
