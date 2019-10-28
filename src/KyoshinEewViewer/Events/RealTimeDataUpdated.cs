using KyoshinMonitorLib;
using Prism.Events;
using System;

namespace KyoshinEewViewer.Events
{
	public class RealTimeDataUpdated : PubSubEvent<RealTimeDataUpdated>
	{
		/// <summary>
		/// 時間
		/// </summary>
		public DateTime Time { get; set; }

		/// <summary>
		/// 観測情報
		/// </summary>
		public LinkedRealTimeData[] Data { get; set; }

		/// <summary>
		/// EEW
		/// </summary>
		public Models.Eew[] Eews { get; set; }

		/// <summary>
		/// 代替ソースを使用したかどうか
		/// </summary>
		public bool IsUseAlternativeSource { get; set; }
	}
}