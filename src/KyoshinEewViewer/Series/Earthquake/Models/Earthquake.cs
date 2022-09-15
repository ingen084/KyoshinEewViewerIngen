using KyoshinMonitorLib;
using ReactiveUI;
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
		_id = id;

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

	private bool _isSelecting;
	public bool IsSelecting
	{
		get => _isSelecting;
		set => this.RaiseAndSetIfChanged(ref _isSelecting, value);
	}

	private string _id;
	public string Id
	{
		get => _id;
		set => this.RaiseAndSetIfChanged(ref _id, value);
	}

	private bool _isSokuhou;
	public bool IsSokuhou
	{
		get => _isSokuhou;
		set => this.RaiseAndSetIfChanged(ref _isSokuhou, value);
	}

	private bool _isOnlypoint;
	public bool IsOnlypoint
	{
		get => _isOnlypoint;
		set => this.RaiseAndSetIfChanged(ref _isOnlypoint, value);
	}

	private bool _isTraining;
	public bool IsTraining
	{
		get => _isTraining;
		set => this.RaiseAndSetIfChanged(ref _isTraining, value);
	}

	private Location? _location;
	public Location? Location
	{
		get => _location;
		set => this.RaiseAndSetIfChanged(ref _location, value);
	}

	private bool _isHypocenterOnly;
	public bool IsHypocenterOnly
	{
		get => _isHypocenterOnly;
		set => this.RaiseAndSetIfChanged(ref _isHypocenterOnly, value);
	}

	private readonly ObservableAsPropertyHelper<bool> isHypocenterAvailable;
	public bool IsHypocenterAvailable => isHypocenterAvailable.Value;

	private DateTime _occurrenceTime;
	public DateTime OccurrenceTime
	{
		get => _occurrenceTime;
		set => this.RaiseAndSetIfChanged(ref _occurrenceTime, value);
	}

	private bool _isTargetTime;
	public bool IsTargetTime
	{
		get => _isTargetTime;
		set => this.RaiseAndSetIfChanged(ref _isTargetTime, value);
	}

	private string? _place;
	public string? Place
	{
		get => _place;
		set => this.RaiseAndSetIfChanged(ref _place, value);
	}

	private JmaIntensity _intensity;
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	private float _magnitude;
	public float Magnitude
	{
		get => _magnitude;
		set => this.RaiseAndSetIfChanged(ref _magnitude, value);
	}

	private string? _magnitudeAlternativeText;
	public string? MagnitudeAlternativeText
	{
		get => _magnitudeAlternativeText;
		set => this.RaiseAndSetIfChanged(ref _magnitudeAlternativeText, value);
	}

	private string? _headlineText;
	public string? HeadlineText
	{
		get => _headlineText;
		set => this.RaiseAndSetIfChanged(ref _headlineText, value);
	}

	private string? _headTitle;
	public string? HeadTitle
	{
		get => _headTitle;
		set => this.RaiseAndSetIfChanged(ref _headTitle, value);
	}

	private string? _comment;
	public string? Comment
	{
		get => _comment;
		set => this.RaiseAndSetIfChanged(ref _comment, value);
	}

	private string? _freeFormComment;
	public string? FreeFormComment
	{
		get => _freeFormComment;
		set => this.RaiseAndSetIfChanged(ref _freeFormComment, value);
	}

	private int _depth;
	public int Depth
	{
		get => _depth;
		set => this.RaiseAndSetIfChanged(ref _depth, value);
	}

	private readonly ObservableAsPropertyHelper<bool> isVeryShallow;
	public bool IsVeryShallow => isVeryShallow.Value;
	private readonly ObservableAsPropertyHelper<bool> isNoDepthData;
	public bool IsNoDepthData => isNoDepthData.Value;

	private readonly ObservableAsPropertyHelper<string?> title;
	public string? Title => title?.Value;

	public string GetNotificationMessage()
	{
		var parts = new List<string>();
		if (IsTraining)
			parts.Add($"[訓練]");
		if (Intensity != JmaIntensity.Unknown)
			parts.Add($"最大{Intensity.ToLongString()}");

		if (IsHypocenterAvailable)
		{
			parts.Insert(0, $"{OccurrenceTime:HH:mm}");
			parts.Add(Place ?? "不明");
			if (!IsNoDepthData)
			{
				if (IsVeryShallow)
					parts.Add("ごく浅い");
				else
					parts.Add(Depth + "km");
			}
			parts.Add(MagnitudeAlternativeText ?? $"M{Magnitude:0.0}");
		}
		return string.Join('/', parts);
	}
}
public record ProcessedTelegram(string Id, DateTime ArrivalTime, string Title)
{
	public string MenuText => $"{Title} {ArrivalTime:HH:mm:ss}発表";
}
