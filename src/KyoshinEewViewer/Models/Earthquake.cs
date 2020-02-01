using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	/// <summary>
	/// 地震情報
	/// </summary>
	public class Earthquake
	{
		public ulong Id { get; set; }
		public bool IsSokuhou { get; set; }
		public bool IsNotSokuhou => !IsSokuhou;
		public DateTime OccurrenceTime { get; set; }
		public bool IsReportTime { get; set; }
		public bool IsNotReportTime => !IsReportTime;
		public string Place { get; set; }
		public JmaIntensity Intensity { get; set; }
		public float Magnitude { get; set; }
		public int Depth { get; set; }
	}
}