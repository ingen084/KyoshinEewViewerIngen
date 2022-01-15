using KyoshinMonitorLib;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 地震情報
/// </summary>
public class Earthquake : ReactiveObject
{
	public Earthquake(string id)
	{
		Id = id;

		isHypocenterAvailable = this.WhenAny(
			x => x.IsHypocenterOnly,
			x => x.IsSokuhou,
			(only, sokuhou) => only.Value || (!only.Value && !sokuhou.Value)
		).ToProperty(this, x => x.IsHypocenterAvailable);

		title = this.WhenAny(
			x => x.IsHypocenterOnly,
			x => x.IsSokuhou,
			(only, sokuhou) =>
			{
				if (sokuhou.Value && only.Value)
					return "震度速報+震源情報";
				if (sokuhou.Value)
					return "震度速報";
				if (only.Value)
					return "震源情報";
				return "震源･震度情報";
			}
		).ToProperty(this, x => x.Title);

		isVeryShallow = this.WhenAny(
			x => x.Depth,
			depth => Depth <= 0
		).ToProperty(this, x => x.IsVeryShallow);

		isNoDepthData = this.WhenAny(
			x => x.Depth,
			depth => depth.Value <= -1
		).ToProperty(this, x => x.IsNoDepthData);
	}

	public List<ProcessedTelegram> UsedModels { get; } = new();

	[Reactive]
	public bool IsSelecting { get; set; }

	[Reactive]
	public string Id { get; set; }

	[Reactive]
	public bool IsSokuhou { get; set; }

	[Reactive]
	public bool IsOnlypoint { get; set; }

	[Reactive]
	public Location? Location { get; set; }

	[Reactive]
	public bool IsHypocenterOnly { get; set; }

	private readonly ObservableAsPropertyHelper<bool> isHypocenterAvailable;
	public bool IsHypocenterAvailable => isHypocenterAvailable.Value;

	[Reactive]
	public DateTime OccurrenceTime { get; set; }

	[Reactive]
	public bool IsReportTime { get; set; }

	[Reactive]
	public string? Place { get; set; }

	[Reactive]
	public JmaIntensity Intensity { get; set; }

	[Reactive]
	public float Magnitude { get; set; }

	[Reactive]
	public string? MagnitudeAlternativeText { get; set; }

	[Reactive]
	public string? Comment { get; set; }

	[Reactive]
	public string? FreeFormComment { get; set; }

	[Reactive]
	public int Depth { get; set; }

	private readonly ObservableAsPropertyHelper<bool> isVeryShallow;
	public bool IsVeryShallow => isVeryShallow.Value;
	private readonly ObservableAsPropertyHelper<bool> isNoDepthData;
	public bool IsNoDepthData => isNoDepthData.Value;

	private readonly ObservableAsPropertyHelper<string?> title;
	public string? Title => title?.Value;

	public string GetNotificationMessage()
	{
		var parts = new List<string>();
		if (Intensity != JmaIntensity.Unknown)
			parts.Add($"最大{Intensity.ToLongString()}");

		if (IsHypocenterAvailable)
		{
			parts.Insert(0, $"{OccurrenceTime:HH:mm}");
			parts.Add(Place ?? "不明");
			parts.Add($"M{Magnitude:0.0}");
		}
		return string.Join('/', parts);
	}
}
public record ProcessedTelegram(string Id, DateTime ArrivalTime, string Title);
