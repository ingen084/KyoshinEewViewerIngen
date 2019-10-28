using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	/// <summary>
	/// 地震情報
	/// </summary>
	public class Earthquake
	{
		public DateTime OccurrenceTime { get; set; }
		public string Place { get; set; }
		public JmaIntensity Intensity { get; set; }
		public float Magnitude { get; set; }
		public int Depth { get; set; }
	}
}