using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public partial class EewTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(EewEvent);

	public static Dictionary<JmaIntensity, string> ShindoNames { get; } = new()
	{
		{ JmaIntensity.Unknown, "震度不明" },
		{ JmaIntensity.Int0, "震度0" },
		{ JmaIntensity.Int1, "震度1" },
		{ JmaIntensity.Int2, "震度2" },
		{ JmaIntensity.Int3, "震度3" },
		{ JmaIntensity.Int4, "震度4" },
		{ JmaIntensity.Int5Lower, "震度5弱" },
		{ JmaIntensity.Int5Upper, "震度5強" },
		{ JmaIntensity.Int6Lower, "震度6弱" },
		{ JmaIntensity.Int6Upper, "震度6強" },
		{ JmaIntensity.Int7, "震度7" },
	};

	[JsonIgnore]
	public override Control DisplayControl => new EewTriggerControl() { DataContext = this };

	[ObservableProperty]
	public partial bool New { get; set; } = true;

	[ObservableProperty]
	public partial bool NewWarning { get; set; } = false;

	[ObservableProperty]
	public partial bool ContinueWarning { get; set; } = false;

	[ObservableProperty]
	public partial bool Continue { get; set; } = true;

	[ObservableProperty]
	public partial bool UpdateWithMoreAccurate { get; set; } = true;

	[ObservableProperty]
	public partial bool Final { get; set; } = true;

	[ObservableProperty]
	public partial bool Cancel { get; set; } = true;

	[ObservableProperty]
	public partial bool CancelWarning { get; set; } = false;

	[ObservableProperty]
	public partial bool WarningLevelReached { get; set; } = false;

	[ObservableProperty]
	public partial bool IncreaseInIntensity { get; set; } = false;

	[ObservableProperty]
	public partial bool DecreaseInIntensity { get; set; } = false;

	[ObservableProperty]
	public partial JmaIntensity Intensity { get; set; } = JmaIntensity.Unknown;

	[ObservableProperty]
	public partial bool IncludeGreaterIntensity { get; set; } = true;

	[ObservableProperty]
	public partial float Magnitude { get; set; } = 0;

	[ObservableProperty]
	public partial bool UseAndCondition { get; set; } = true;

	/// <summary>
	/// 同じEEWでは条件を満たした最初の1回だけトリガーするか
	/// </summary>
	[ObservableProperty]
	public partial bool OncePerEew { get; set; } = false;

	/// <summary>
	/// 訓練でもトリガーするか
	/// </summary>
	[ObservableProperty]
	public partial bool IncludeTraining { get; set; } = false;

	/// <summary>
	/// 一度トリガーしたEEWの記録を保持する期間
	/// </summary>
	private static readonly TimeSpan OncePerEewRetention = TimeSpan.FromHours(24);

	private readonly Lock _triggeredEewLock = new();
	private readonly HashSet<string> _triggeredEewKeys = [];
	private readonly Queue<(string Key, DateTime TriggeredAt)> _triggeredEewHistory = [];

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not EewEvent eewEvent)
			return false;

		if (eewEvent.IsTraining && !IncludeTraining)
			return false;

		// 震度条件のチェック
		bool intensityMatch;
		if (IncludeGreaterIntensity)
			intensityMatch = eewEvent.Intensity >= Intensity;
		else
			intensityMatch = eewEvent.Intensity == Intensity;

		// マグニチュード条件のチェック (M0以上がデフォルトなので常に以上で比較)
		var magnitudeMatch = eewEvent.Magnitude >= Magnitude;

		// AND/OR条件の適用
		var conditionMatch = UseAndCondition
			? intensityMatch && magnitudeMatch
			: intensityMatch || magnitudeMatch;

		if (!conditionMatch)
			return false;

		var eventSubTypeMatch = eewEvent.EventSubType switch
		{
			EewEventType.New => New,
			EewEventType.UpdateNewSerial => Continue,
			EewEventType.UpdateWithMoreAccurate => UpdateWithMoreAccurate,
			EewEventType.Final => Final,
			EewEventType.Cancel => Cancel,
			EewEventType.NewWarning => NewWarning,
			EewEventType.UpdateWarning => ContinueWarning,
			EewEventType.CancelWarning => CancelWarning,
			EewEventType.WarningLevelReached => WarningLevelReached,
			EewEventType.IncreaseMaxIntensity => IncreaseInIntensity,
			EewEventType.DecreaseMaxIntensity => DecreaseInIntensity,
			_ => false,
		};
		if (!eventSubTypeMatch)
			return false;

		// 同じEEWでは条件を満たした最初の1回だけトリガーする
		return !OncePerEew || TryMarkEewTriggered(eewEvent);
	}

	/// <summary>
	/// 同じEEWでまだトリガーしていない場合のみ記録して true を返す
	/// </summary>
	private bool TryMarkEewTriggered(EewEvent eewEvent)
	{
		var key = Eew.GetKey(eewEvent.EewId, eewEvent.IsTraining);
		var now = DateTime.UtcNow;
		lock (_triggeredEewLock)
		{
			// 保持期間を過ぎた記録を破棄する
			while (_triggeredEewHistory.TryPeek(out var oldest) && now - oldest.TriggeredAt >= OncePerEewRetention)
			{
				_triggeredEewHistory.Dequeue();
				_triggeredEewKeys.Remove(oldest.Key);
			}

			if (!_triggeredEewKeys.Add(key))
				return false;

			_triggeredEewHistory.Enqueue((key, now));
			return true;
		}
	}

	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();

		var eventTypes = new List<EewEventType>();
		if (New)
			eventTypes.Add(EewEventType.New);
		if (NewWarning)
			eventTypes.Add(EewEventType.NewWarning);
		if (Continue)
			eventTypes.Add(EewEventType.UpdateNewSerial);
		if (UpdateWithMoreAccurate)
			eventTypes.Add(EewEventType.UpdateWithMoreAccurate);
		if (Final)
			eventTypes.Add(EewEventType.Final);
		if (Cancel)
			eventTypes.Add(EewEventType.Cancel);

		EewEventType eventType = 0;
		if (eventTypes.Count > 0)
			eventType = eventTypes[random.Next(eventTypes.Count)];

		// 震度を条件に合わせて生成
		JmaIntensity intensity;
		if (IncludeGreaterIntensity)
			intensity = random.Next(JmaIntensity.Error - Intensity) + Intensity;
		else
			intensity = Intensity;

		// マグニチュードを条件に合わせて生成 (Magnitude以上のランダムな値)
		var magnitude = Magnitude + random.NextSingle() * (10 - Magnitude);

		return new EewEvent(null, eventType)
		{
			IsTest = true,

			OccurrenceAt = DateTime.Now.AddSeconds(-random.Next(60)),

			EewId = DateTime.Now.ToString("yyyyMMddHHmmss"),
			EewSource = "ワークフローのテストボタン",

			SerialNo = random.Next(20),

			IsTrueCancelled = eventType == EewEventType.Cancel ? random.Next() % 2 == 0 : false,

			Intensity = intensity,
			IsIntensityOver = intensity != JmaIntensity.Unknown && eventType != EewEventType.Cancel && random.Next() % 2 == 0,

			EpicenterPlaceName = "テスト",
			EpicenterLocation = new(random.NextSingle() * 180 - 90, random.NextSingle() * 360 - 180),

			Magnitude = magnitude,
			Depth = random.Next(20) * 10,

			IsTemporaryEpicenter = random.Next() % 2 == 0,

			IsWarning = intensity >= JmaIntensity.Int5Lower,
			WarningAreaCodes = intensity >= JmaIntensity.Int5Lower ? [9999] : null,
			WarningAreaNames = intensity >= JmaIntensity.Int5Lower ? ["テスト"] : null,
		};
	}
}
