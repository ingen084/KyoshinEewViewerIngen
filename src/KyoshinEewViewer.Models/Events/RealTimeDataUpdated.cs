using KyoshinMonitorLib;
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
		public LinkedRealtimeData[] Data { get; set; }

		/// <summary>
		/// 代替ソースを使用したかどうか
		/// </summary>
		public bool IsUseAlternativeSource { get; set; }
	}
}