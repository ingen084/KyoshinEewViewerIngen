using KyoshinMonitorLib.Images;
using Prism.Events;
using System;

namespace KyoshinEewViewer.Models.Events
{
	public class RealtimeDataUpdated : PubSubEvent<RealtimeDataUpdated>
	{
		/// <summary>
		/// 時間
		/// </summary>
		public DateTime Time { get; set; }

		/// <summary>
		/// 観測情報
		/// </summary>
		public ImageAnalysisResult[] Data { get; set; }
	}
}