using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Workflow;

public class EarthquakeInformationTrigger : WorkflowTrigger
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

	public override Control DisplayControl => new EarthquakeInformationTriggerControl { DataContext = this };

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	private bool _isIntensityChangeOnly;
	public bool IsIntensityChangeOnly
	{
		get => _isIntensityChangeOnly;
		set => this.RaiseAndSetIfChanged(ref _isIntensityChangeOnly, value);
	}

	private bool _isIntensityIncreaseOnly = true;
	public bool IsIntensityIncreaseOnly
	{
		get => _isIntensityIncreaseOnly;
		set => this.RaiseAndSetIfChanged(ref _isIntensityIncreaseOnly, value);
	}

	private bool _enableSokuhou = true;
	public bool EnableSokuhou
	{
		get => _enableSokuhou;
		set => this.RaiseAndSetIfChanged(ref _enableSokuhou, value);
	}

	private bool _enableEpicenter = true;
	public bool EnableEpicenter
	{
		get => _enableEpicenter;
		set => this.RaiseAndSetIfChanged(ref _enableEpicenter, value);
	}

	private bool _enableDetail = true;
	public bool EnableDetail
	{
		get => _enableDetail;
		set => this.RaiseAndSetIfChanged(ref _enableDetail, value);
	}

	private bool _enableUpdateEpicenter = true;
	public bool EnableUpdateEpicenter
	{
		get => _enableUpdateEpicenter;
		set => this.RaiseAndSetIfChanged(ref _enableUpdateEpicenter, value);
	}

	private bool _enableTsunami = true;
	public bool EnableTsunami
	{
		get => _enableTsunami;
		set => this.RaiseAndSetIfChanged(ref _enableTsunami, value);
	}

	private bool _enableLpgm = true;
	public bool EnableLpgm
	{
		get => _enableLpgm;
		set => this.RaiseAndSetIfChanged(ref _enableLpgm, value);
	}

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not EarthquakeInformationEvent e)
			return false;

		if (e.MaxIntensity < Intensity)
			return false;

		if (IsIntensityChangeOnly)
		{
			if (e.PreviousMaxIntensity != null && (
				e.MaxIntensity == e.PreviousMaxIntensity ||
				(IsIntensityIncreaseOnly && e.MaxIntensity <= e.PreviousMaxIntensity)
			))
				return false;
			return true;
		}

		return e.LatestInformationName switch
		{
			"震度速報" => EnableSokuhou,
			"震源に関する情報" => EnableEpicenter,
			"震源・震度に関する情報" => EnableDetail,
			"顕著な地震の震源要素更新のお知らせ" => EnableUpdateEpicenter,
			"津波警報・注意報・予報a" => EnableTsunami,
			"長周期地震動に関する観測情報" => EnableLpgm,
			_ => false
		};
	}

	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		return new EarthquakeInformationEvent
		{
			UpdatedAt = DateTime.Now,
			LatestInformationName = "テスト情報",
			EarthquakeId = DateTime.Now.ToString("yyyyMMddHHmmss"),
			IsTrainingOrTest = true,
			DetectedAt = DateTime.Now,
			MaxIntensity = (JmaIntensity)random.Next(1, 8),
			PreviousMaxIntensity = (JmaIntensity)random.Next(1, 8),
			MaxLpgmIntensity = (LpgmIntensity)random.Next(1, 5),
			Hypocenter = new(
				DateTime.Now,
				"テスト地点",
				new(0, 0),
				random.Next(1, 8),
				null,
				random.Next(1, 100),
				random.Next(0, 1) == 1
			),
		};
	}
}
