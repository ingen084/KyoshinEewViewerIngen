using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	/// <summary>
	/// 地震情報
	/// </summary>
	public class Earthquake
	{
		public string Id { get; set; }
		public bool IsSokuhou { get; set; }
		public bool IsNotSokuhou => !IsSokuhou;
		public DateTime OccurrenceTime { get; set; }
		public string Place { get; set; }
		public bool IsManyPlace { get; set; }
		public JmaIntensity Intensity { get; set; }
		public float Magnitude { get; set; }
		public int Depth { get; set; }
	}
}