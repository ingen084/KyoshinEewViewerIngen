using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Earthquake.Models;
using System;
using System.Collections.ObjectModel;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 信頼できないソース（P2P地震情報など）のイベント管理
/// 同一地震判定、信頼ソースとの突合、打ち消しを担う
/// </summary>
public class UnreliableEventManager(
	ObservableCollection<EarthquakeEvent> earthquakes,
	KyoshinEewViewerConfiguration config)
{
	/// <summary>
	/// 現在の信頼できないイベント（最大1件保持）
	/// </summary>
	public EarthquakeEvent? CurrentEvent { get; private set; }

	/// <summary>
	/// 現在の信頼できないイベントをクリアする
	/// </summary>
	public void Reset() => CurrentEvent = null;

	/// <summary>
	/// 信頼できないイベントを登録する
	/// </summary>
	public void SetCurrentEvent(EarthquakeEvent eq) => CurrentEvent = eq;

	/// <summary>
	/// 保持モードでない場合に現在の信頼できないイベントを削除する
	/// </summary>
	public void RemoveCurrentIfNotKeeping()
	{
		if (CurrentEvent == null)
			return;
		if (!config.P2pQuakeApi.KeepEarthquakeEvents)
			earthquakes.Remove(CurrentEvent);
		CurrentEvent = null;
	}

	/// <summary>
	/// 信頼できるソースのデータで打ち消し判定を行う
	/// </summary>
	public void TryDismiss(EarthquakeInformationData data, EarthquakeEvent reliableEvent)
	{
		if (CurrentEvent == null)
			return;
		if (config.P2pQuakeApi.KeepEarthquakeEvents)
			return;
		if (reliableEvent.IsUnreliableEventIdSource)
			return;

		// 震度速報か否かの一致を確認
		var unreliableIsSokuhou = CurrentEvent.IsSokuhou && !CurrentEvent.IsHypocenterOnly;
		var receivedIsSokuhou = data.Title == "震度速報";
		if (unreliableIsSokuhou != receivedIsSokuhou)
			return;

		if (CurrentEvent.Time != reliableEvent.Time)
			return;
		if (CurrentEvent.Magnitude != reliableEvent.Magnitude)
			return;
		if (CurrentEvent.Place != reliableEvent.Place)
			return;

		earthquakes.Remove(CurrentEvent);
		CurrentEvent = null;
	}

	/// <summary>
	/// 信頼できるソースで同等の地震情報が既に存在するかを判定する
	/// </summary>
	public bool HasMatchingReliableEvent(EarthquakeInformationData data)
	{
		var isSokuhou = data.Title == "震度速報";
		var newTime = data.Hypocenter?.OccurrenceTime ?? data.Intensity?.DetectionTime;
		if (newTime == null)
			return false;

		foreach (var eq in earthquakes)
		{
			if (eq.IsUnreliableEventIdSource)
				continue;

			if (Math.Abs((eq.Time - newTime.Value).TotalMinutes) >= 5)
				continue;

			if (isSokuhou)
			{
				if (eq.IsSokuhou || eq.IsDetailIntensityApplied)
					return true;
				continue;
			}

			if (data.Hypocenter != null && eq.IsHypocenterAvailable && data.Hypocenter.Place == eq.Place)
				return true;
		}
		return false;
	}

	/// <summary>
	/// 現在の信頼できないイベントと同一地震の続報かどうかを判定する
	/// P2P地震情報では震度速報と震源情報で時刻がずれることがあるため、余裕を持って判定する
	/// </summary>
	public bool IsSameEarthquake(EarthquakeInformationData newData)
	{
		if (CurrentEvent == null)
			return false;

		var newTime = newData.Hypocenter?.OccurrenceTime ?? newData.Intensity?.DetectionTime;
		if (newTime == null)
			return false;

		if (Math.Abs((CurrentEvent.Time - newTime.Value).TotalMinutes) >= 5)
			return false;

		// 震度速報は時刻の一致のみで判定
		if (newData.Title == "震度速報")
			return true;

		// 震度速報以外で既存イベントに震源情報がある場合は震央地名の一致を確認
		if (newData.Hypocenter != null && CurrentEvent.IsHypocenterAvailable && CurrentEvent.Place != null)
			return newData.Hypocenter.Place == CurrentEvent.Place;

		return true;
	}
}
