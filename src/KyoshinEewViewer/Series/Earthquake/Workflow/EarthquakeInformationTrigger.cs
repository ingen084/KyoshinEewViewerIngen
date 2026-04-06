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
	public override Type EventType => typeof(EarthquakeInformationEvent);

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

	public override Control DisplayControl => new EarthquakeInformationTriggerControl { DataContext = this };

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	private bool _includeGreaterIntensity = true;
	public bool IncludeGreaterIntensity
	{
		get => _includeGreaterIntensity;
		set => this.RaiseAndSetIfChanged(ref _includeGreaterIntensity, value);
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

		if (IncludeGreaterIntensity)
		{
			if (e.MaxIntensity < Intensity)
				return false;
		}
		else
		{
			if (e.MaxIntensity != Intensity)
				return false;
		}

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

		// 設定に合わせた最大震度を決定
		JmaIntensity maxIntensity;
		if (IncludeGreaterIntensity)
		{
			// 「以上を含む」がオンの場合、設定された震度以上の値をランダムに選択
			var minValue = (int)Intensity;
			var maxValue = (int)JmaIntensity.Int7;
			maxIntensity = minValue <= maxValue
				? (JmaIntensity)random.Next(minValue, maxValue + 1)
				: Intensity;
		}
		else
		{
			// 「以上を含む」がオフの場合、設定された震度を使用
			maxIntensity = Intensity;
		}

		// 前回の最大震度を決定（震度変更時トリガーを考慮）
		JmaIntensity? previousMaxIntensity = null;
		if (IsIntensityChangeOnly && IsIntensityIncreaseOnly && maxIntensity > JmaIntensity.Int0)
		{
			// 震度上昇時のみの場合、前回は今回より低い震度
			previousMaxIntensity = (JmaIntensity)random.Next(0, (int)maxIntensity);
		}
		else if (IsIntensityChangeOnly)
		{
			// 震度変更時の場合、今回と異なる震度
			var prev = (JmaIntensity)random.Next(0, (int)JmaIntensity.Int7 + 1);
			previousMaxIntensity = prev != maxIntensity ? prev : null;
		}

		// 有効な情報種別からランダムに選択
		var enabledInfoNames = new List<string>();
		if (EnableSokuhou) enabledInfoNames.Add("震度速報");
		if (EnableEpicenter) enabledInfoNames.Add("震源に関する情報");
		if (EnableDetail) enabledInfoNames.Add("震源・震度に関する情報");
		if (EnableUpdateEpicenter) enabledInfoNames.Add("顕著な地震の震源要素更新のお知らせ");
		if (EnableTsunami) enabledInfoNames.Add("津波警報・注意報・予報");
		if (EnableLpgm) enabledInfoNames.Add("長周期地震動に関する観測情報");

		var latestInfoName = enabledInfoNames.Count > 0
			? enabledInfoNames[random.Next(enabledInfoNames.Count)]
			: "テスト情報";

		return new EarthquakeInformationEvent(null)
		{
			IsTest = true,
			UpdatedAt = DateTime.Now,
			LatestInformationName = latestInfoName,
			EarthquakeId = DateTime.Now.ToString("yyyyMMddHHmmss"),
			IsTrainingOrTest = true,
			DetectedAt = DateTime.Now,
			MaxIntensity = maxIntensity,
			PreviousMaxIntensity = previousMaxIntensity,
			MaxLpgmIntensity = (LpgmIntensity)random.Next(1, 5),
			Hypocenter = new(
				DateTime.Now,
				"テスト地点",
				new(0, 0),
				random.Next(1, 8),
				null,
				random.Next(1, 100),
				random.Next(0, 1) == 1,
				random.Next(0, 1) == 1,
				random.Next(0, 1) == 1
			),
		};
	}
}
