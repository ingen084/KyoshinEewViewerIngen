using KyoshinMonitorLib.SkiaImages;
using System;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class RealtimeDataUpdated
	{
		public RealtimeDataUpdated(DateTime time, ImageAnalysisResult[] data)
		{
			Time = time;
			Data = data;
		}

		/// <summary>
		/// 時間
		/// </summary>
		public DateTime Time { get; }

		/// <summary>
		/// 観測情報
		/// </summary>
		public ImageAnalysisResult[] Data { get; }
	}
}