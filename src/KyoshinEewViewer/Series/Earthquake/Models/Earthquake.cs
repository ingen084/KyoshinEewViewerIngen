using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Models
{
	/// <summary>
	/// 地震情報
	/// </summary>
	public class Earthquake : ReactiveObject
	{
		public Earthquake(string id)
		{
			Id = id;
		}

		public List<InformationHeader> UsedModels { get; } = new();

		[Reactive]
		public bool IsSelecting { get; set; }

		[Reactive]
		public string Id { get; set; }

		private bool isSokuhou;
		public bool IsSokuhou
		{
			get => isSokuhou;
			set
			{
				this.RaiseAndSetIfChanged(ref isSokuhou, value);
				this.RaisePropertyChanged(nameof(Title));
				this.RaisePropertyChanged(nameof(IsHypocenterAvailable));
			}
		}
		private bool isHypocenterOnly;
		public bool IsHypocenterOnly
		{
			get => isHypocenterOnly;
			set
			{
				this.RaiseAndSetIfChanged(ref isHypocenterOnly, value);
				this.RaisePropertyChanged(nameof(Title));
				this.RaisePropertyChanged(nameof(IsHypocenterAvailable));
			}
		}
		public bool IsHypocenterAvailable => IsHypocenterOnly || (!IsHypocenterOnly && !IsSokuhou);

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

		private int depth;
		[Reactive]
		public int Depth 
		{
			get => depth;
			set
			{
				this.RaiseAndSetIfChanged(ref depth, value);
				this.RaisePropertyChanged(nameof(IsVeryShallow));
				this.RaisePropertyChanged(nameof(IsNoDepthData));
			}
		}

		public string Title
		{
			get
			{
				if (IsSokuhou && IsHypocenterOnly)
					return "震度速報+震源情報";
				if (IsSokuhou)
					return "震度速報";
				if (IsHypocenterOnly)
					return "震源情報";
				return "震源･震度情報";
			}
		}
		public bool IsVeryShallow => Depth <= 0;
		public bool IsNoDepthData => Depth <= -1;
	}
}