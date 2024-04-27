using Avalonia.Controls;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
public class EewTrigger : WorkflowTrigger
{
	public static Dictionary<JmaIntensity, string> ShindoNames { get; } = new()
	{
		{ JmaIntensity.Unknown, "すべて(震度0含む)" },
		{ JmaIntensity.Int1, "震度1以上" },
		{ JmaIntensity.Int2, "震度2以上" },
		{ JmaIntensity.Int3, "震度3以上" },
		{ JmaIntensity.Int4, "震度4以上" },
		{ JmaIntensity.Int5Lower, "震度5弱以上" },
		{ JmaIntensity.Int5Upper, "震度5強以上" },
		{ JmaIntensity.Int6Lower, "震度6弱以上" },
		{ JmaIntensity.Int6Upper, "震度6強以上" },
		{ JmaIntensity.Int7, "震度7以上" },
	};

	public override Control DisplayControl => new EewTriggerControl() { DataContext = this };

	private bool _new = true;
	public bool New
	{
		get => _new;
		set => this.RaiseAndSetIfChanged(ref _new, value);
	}

	private bool _newWarning = false;
	public bool NewWarning
	{
		get => _newWarning;
		set => this.RaiseAndSetIfChanged(ref _newWarning, value);
	}

	private bool _continue = true;
	public bool Continue
	{
		get => _continue;
		set => this.RaiseAndSetIfChanged(ref _continue, value);
	}

	private bool _updateWithMoreAccurate = true;
	public bool UpdateWithMoreAccurate
	{
		get => _updateWithMoreAccurate;
		set => this.RaiseAndSetIfChanged(ref _updateWithMoreAccurate, value);
	}

	private bool _final = true;
	public bool Final
	{
		get => _final;
		set => this.RaiseAndSetIfChanged(ref _final, value);
	}

	private bool _cancel = true;
	public bool Cancel
	{
		get => _cancel;
		set => this.RaiseAndSetIfChanged(ref _cancel, value);
	}

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not EewEvent eewEvent)
			return false;

		return eewEvent.EventSubType switch
		{
			EewEventType.New => New,
			EewEventType.UpdateNewSerial => Continue,
			EewEventType.UpdateWithMoreAccurate => UpdateWithMoreAccurate,
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

		var intensity = random.Next(JmaIntensity.Error - Intensity) + Intensity;
		return new EewEvent(eventType)
		{
			IsTest = true,

			OccurrenceAt = DateTime.Now.AddSeconds(-random.Next(60)),

			EewId = DateTime.Now.ToString("yyyyMMddHHmmss"),
			EewSource = "ワークフローのテストボタン",

			Serial = random.Next(20),

			IsTrueCancelled = eventType == EewEventType.Cancel ? random.Next() % 2 == 0 : false,

			Intensity = intensity,
			IsIntensityOver = intensity != JmaIntensity.Unknown && eventType != EewEventType.Cancel && random.Next() % 2 == 0,

			EpicenterPlaceName = "テスト",
			EpicenterLocation = new(random.NextSingle() * 180 - 90, random.NextSingle() * 360 - 180),

			Magnitude = random.Next(7) / 10f,
			Depth = random.Next(20) * 10,

			IsTemporaryEpicenter = random.Next() % 2 == 0,

			IsWarning = intensity >= JmaIntensity.Int5Lower,
			WarningAreaCodes = intensity >= JmaIntensity.Int5Lower ? [9999] : null,
			WarningAreaNames = intensity >= JmaIntensity.Int5Lower ? ["テスト"] : null,
		};
	}
}
