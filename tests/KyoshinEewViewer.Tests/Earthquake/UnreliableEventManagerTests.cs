using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinMonitorLib;
using System.Collections.ObjectModel;

namespace KyoshinEewViewer.Tests.Earthquake;

public class UnreliableEventManagerTests
{
	private static readonly DateTime BaseTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

	private static KyoshinEewViewerConfiguration CreateConfig(bool keepEvents = false)
	{
		var config = new KyoshinEewViewerConfiguration();
		config.P2pQuakeApi.KeepEarthquakeEvents = keepEvents;
		return config;
	}

	private static EarthquakeEvent CreateEvent(
		string eventId,
		DateTime? time = null,
		string? place = null,
		float magnitude = 4.0f,
		bool isSokuhou = false,
		bool isUnreliable = false)
	{
		var eq = new EarthquakeEvent(eventId);
		// ProcessIntermediateData でプロパティを設定する
		var data = new EarthquakeInformationData
		{
			Title = isSokuhou ? "震度速報" : "震源・震度に関する情報",
			EventId = eventId,
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = time ?? BaseTime,
			Source = "Test",
			Hypocenter = isSokuhou ? null : new EarthquakeHypocenterData
			{
				OccurrenceTime = time ?? BaseTime,
				Place = place ?? "テスト震源",
				Location = new Location(35.0f, 139.0f),
				Magnitude = magnitude,
				Depth = 10,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = JmaIntensity.Int3,
				DetectionTime = time ?? BaseTime,
				RepresentativeAreaName = place ?? "テスト地域",
				IsOnlyArea = true,
			},
		};
		eq.ProcessIntermediateData(data);
		eq.IsUnreliableEventIdSource = isUnreliable;
		return eq;
	}

	private static EarthquakeInformationData CreateData(
		string title = "震源・震度に関する情報",
		DateTime? time = null,
		string? place = null,
		float magnitude = 4.0f)
	{
		return new EarthquakeInformationData
		{
			Title = title,
			EventId = "new-event",
			InfoType = EarthquakeInfoType.Normal,
			Status = EarthquakeReportStatus.Normal,
			ReportDateTime = time ?? BaseTime,
			Source = "Test",
			Hypocenter = title == "震度速報" ? null : new EarthquakeHypocenterData
			{
				OccurrenceTime = time ?? BaseTime,
				Place = place ?? "テスト震源",
				Location = new Location(35.0f, 139.0f),
				Magnitude = magnitude,
				Depth = 10,
			},
			Intensity = new EarthquakeIntensityData
			{
				MaxIntensity = JmaIntensity.Int3,
				DetectionTime = time ?? BaseTime,
				RepresentativeAreaName = "テスト地域",
				IsOnlyArea = true,
			},
		};
	}

	[Fact(DisplayName = "同一地震の続報を正しく判定する")]
	public void IsSameEarthquake_SameEarthquake_ReturnsTrue()
	{
		var earthquakes = new ObservableCollection<EarthquakeEvent>();
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());
		var existing = CreateEvent("event-1", BaseTime, "テスト震源");
		manager.SetCurrentEvent(existing);

		var data = CreateData(time: BaseTime.AddMinutes(1), place: "テスト震源");
		Assert.True(manager.IsSameEarthquake(data));
	}

	[Fact(DisplayName = "5分以上離れた地震は異なると判定する")]
	public void IsSameEarthquake_TimeDifferenceOver5Min_ReturnsFalse()
	{
		var earthquakes = new ObservableCollection<EarthquakeEvent>();
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());
		var existing = CreateEvent("event-1", BaseTime, "テスト震源");
		manager.SetCurrentEvent(existing);

		var data = CreateData(time: BaseTime.AddMinutes(6), place: "テスト震源");
		Assert.False(manager.IsSameEarthquake(data));
	}

	[Fact(DisplayName = "震度速報は時刻のみで同一判定する")]
	public void IsSameEarthquake_IntensityReport_MatchesByTimeOnly()
	{
		var earthquakes = new ObservableCollection<EarthquakeEvent>();
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());
		var existing = CreateEvent("event-1", BaseTime, "東京都", isSokuhou: true);
		manager.SetCurrentEvent(existing);

		var data = CreateData(title: "震度速報", time: BaseTime.AddMinutes(2));
		Assert.True(manager.IsSameEarthquake(data));
	}

	[Fact(DisplayName = "信頼できるソースで同等の地震があればtrueを返す")]
	public void HasMatchingReliableEvent_WithMatch_ReturnsTrue()
	{
		var reliable = CreateEvent("reliable-1", BaseTime, "テスト震源", isUnreliable: false);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { reliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());

		var data = CreateData(time: BaseTime, place: "テスト震源");
		Assert.True(manager.HasMatchingReliableEvent(data));
	}

	[Fact(DisplayName = "信頼できないソースのみの場合はfalseを返す")]
	public void HasMatchingReliableEvent_OnlyUnreliable_ReturnsFalse()
	{
		var unreliable = CreateEvent("unreliable-1", BaseTime, "テスト震源", isUnreliable: true);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { unreliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());

		var data = CreateData(time: BaseTime, place: "テスト震源");
		Assert.False(manager.HasMatchingReliableEvent(data));
	}

	[Fact(DisplayName = "信頼できるソースで打ち消しが行われる")]
	public void TryDismiss_MatchingReliableEvent_RemovesUnreliable()
	{
		var unreliable = CreateEvent("unreliable-1", BaseTime, "テスト震源", magnitude: 4.0f, isUnreliable: true);
		var reliable = CreateEvent("reliable-1", BaseTime, "テスト震源", magnitude: 4.0f, isUnreliable: false);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { unreliable, reliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig());
		manager.SetCurrentEvent(unreliable);

		var data = CreateData(time: BaseTime, place: "テスト震源", magnitude: 4.0f);
		manager.TryDismiss(data, reliable);

		Assert.Null(manager.CurrentEvent);
		Assert.DoesNotContain(unreliable, earthquakes);
	}

	[Fact(DisplayName = "保持モード時は打ち消しが行われない")]
	public void TryDismiss_KeepMode_DoesNotRemove()
	{
		var unreliable = CreateEvent("unreliable-1", BaseTime, "テスト震源", magnitude: 4.0f, isUnreliable: true);
		var reliable = CreateEvent("reliable-1", BaseTime, "テスト震源", magnitude: 4.0f, isUnreliable: false);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { unreliable, reliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig(keepEvents: true));
		manager.SetCurrentEvent(unreliable);

		var data = CreateData(time: BaseTime, place: "テスト震源", magnitude: 4.0f);
		manager.TryDismiss(data, reliable);

		Assert.NotNull(manager.CurrentEvent);
		Assert.Contains(unreliable, earthquakes);
	}

	[Fact(DisplayName = "RemoveCurrentIfNotKeepingで保持モードでない場合に削除される")]
	public void RemoveCurrentIfNotKeeping_NotKeeping_Removes()
	{
		var unreliable = CreateEvent("unreliable-1", isUnreliable: true);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { unreliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig(keepEvents: false));
		manager.SetCurrentEvent(unreliable);

		manager.RemoveCurrentIfNotKeeping();

		Assert.Null(manager.CurrentEvent);
		Assert.Empty(earthquakes);
	}

	[Fact(DisplayName = "RemoveCurrentIfNotKeepingで保持モード時は削除されない")]
	public void RemoveCurrentIfNotKeeping_Keeping_DoesNotRemoveFromCollection()
	{
		var unreliable = CreateEvent("unreliable-1", isUnreliable: true);
		var earthquakes = new ObservableCollection<EarthquakeEvent> { unreliable };
		var manager = new UnreliableEventManager(earthquakes, CreateConfig(keepEvents: true));
		manager.SetCurrentEvent(unreliable);

		manager.RemoveCurrentIfNotKeeping();

		Assert.Null(manager.CurrentEvent);
		Assert.Single(earthquakes);
	}
}
