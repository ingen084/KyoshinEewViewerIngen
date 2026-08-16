using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class EewTrigger : WorkflowTrigger
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

	private bool _new = true;
	public bool New
	{
		get => _new;
		set => SetProperty(ref _new, value);
	}

	private bool _newWarning = false;
	public bool NewWarning
	{
		get => _newWarning;
		set => SetProperty(ref _newWarning, value);
	}

	private bool _continueWarning = false;
	public bool ContinueWarning
	{
		get => _continueWarning;
		set => SetProperty(ref _continueWarning, value);
	}

	private bool _continue = true;
	public bool Continue
	{
		get => _continue;
		set => SetProperty(ref _continue, value);
	}

	private bool _updateWithMoreAccurate = true;
	public bool UpdateWithMoreAccurate
	{
		get => _updateWithMoreAccurate;
		set => SetProperty(ref _updateWithMoreAccurate, value);
	}

	private bool _final = true;
	public bool Final
	{
		get => _final;
		set => SetProperty(ref _final, value);
	}

	private bool _cancel = true;
	public bool Cancel
	{
		get => _cancel;
		set => SetProperty(ref _cancel, value);
	}

	private bool _cancelWarning = false;
	public bool CancelWarning
	{
		get => _cancelWarning;
		set => SetProperty(ref _cancelWarning, value);
	}

	private bool _warningLevelReached = false;
	public bool WarningLevelReached
	{
		get => _warningLevelReached;
		set => SetProperty(ref _warningLevelReached, value);
	}

	private bool _increaseInIntensity = false;
	public bool IncreaseInIntensity
	{
		get => _increaseInIntensity;
		set => SetProperty(ref _increaseInIntensity, value);
	}

	private bool _decreaseInIntensity = false;
	public bool DecreaseInIntensity
	{
		get => _decreaseInIntensity;
		set => SetProperty(ref _decreaseInIntensity, value);
	}

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => SetProperty(ref _intensity, value);
	}

	private bool _includeGreaterIntensity = true;
	public bool IncludeGreaterIntensity
	{
		get => _includeGreaterIntensity;
		set => SetProperty(ref _includeGreaterIntensity, value);
	}

	private float _magnitude = 0;
	public float Magnitude
	{
		get => _magnitude;
		set => SetProperty(ref _magnitude, value);
	}

	private bool _useAndCondition = true;
	public bool UseAndCondition
	{
		get => _useAndCondition;
		set => SetProperty(ref _useAndCondition, value);
	}

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not EewEvent eewEvent)
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

		return eewEvent.EventSubType switch
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
