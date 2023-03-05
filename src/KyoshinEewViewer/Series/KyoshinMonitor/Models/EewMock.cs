using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

public class EewMock : IEew
{
	public string Id { get; set; } = "a";
	public string SourceDisplay { get; set; } = "大阪管区気象台";
	public bool IsCancelled { get; set; }
	public bool IsTrueCancelled { get; set; }
	public DateTime ReceiveTime { get; set; } = DateTime.Now;
	public JmaIntensity Intensity { get; set; } = JmaIntensity.Int3;
	public bool IsIntensityOver { get; set; } = false;
	public DateTime OccurrenceTime { get; set; } = DateTime.Now;
	public string? Place { get; set; } = "通常テスト";
	public Location? Location { get; set; }
	public float? Magnitude { get; set; } = 9.9f;
	public int Depth { get; set; } = 999;
	public int Count { get; set; } = 999;
	public bool IsWarning { get; set; }
	public bool IsFinal { get; set; }
	public bool IsAccuracyFound => LocationAccuracy != null && DepthAccuracy != null && MagnitudeAccuracy != null;
	public int? LocationAccuracy { get; set; } = 1;
	public int? DepthAccuracy { get; set; } = 1;
	public int? MagnitudeAccuracy { get; set; } = 1;
	public bool IsTemporaryEpicenter { get; set; }
	public bool? IsLocked { get; set; } = true;
	public Dictionary<int, JmaIntensity>? ForecastIntensityMap { get; set; }
	public int[]? WarningAreaCodes { get; set; }
	public string[]? WarningAreaNames { get; set; }
	public int Priority => 0;
	public DateTime UpdatedTime { get; set; }
}
