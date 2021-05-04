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
		[Reactive]
		public bool IsSokuhou { get; set; }
		[Reactive]
		public bool IsHypocenterOnly { get; set; }
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
		public int Depth { get; set; }

		public bool IsVeryShallow => Depth <= 0;
		public bool IsNoDepthData => Depth <= -1;
	}
}