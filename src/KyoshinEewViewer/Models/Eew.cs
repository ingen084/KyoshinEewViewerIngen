using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	public class Eew
	{
		public bool IsCancelled { get; set; }
		public bool IsNotCancelled => !IsCancelled;
		public DateTime ReceiveTime { get; set; }
		public JmaIntensity Intensity { get; set; }
		public DateTime OccurrenceTime { get; set; }
		public string Place { get; set; }
		public Location Location { get; set; }
		public float Magnitude { get; set; }
		public int Depth { get; set; }
		public int Count { get; set; }
		public bool IsWarning { get; set; }
		public bool IsFinal { get; set; }

		public string WarningString => IsWarning ? "Warning" : "";
		public string Title => $"緊急地震速報({(IsFinal ? "最終" : $"第{(IsCancelled ? "--" : Count.ToString("d2"))}")}報) {ReceiveTime.ToString("HH:mm:ss")}受信";
		public string PlaceString => IsCancelled ? "キャンセルされました" : Place;
	}
}