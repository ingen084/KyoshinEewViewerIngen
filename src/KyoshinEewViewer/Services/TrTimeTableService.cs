using KyoshinEewViewer.Models;
using KyoshinEewViewer.Properties;
using MessagePack;
using System;
using System.Linq;

namespace KyoshinEewViewer.Services
{
	public class TrTimeTableService
	{
		private TrTimeTableItem[] TimeTable { get; set; }

		public (double? pDistance, double? sDistance) CalcDistance(DateTime occurranceTime, DateTime currentTime, int depth)
		{
			if (!TimeTable?.Any(t => t.Distance == depth) ?? true)
				return (null, null);
			var elapsedTime = (currentTime - occurranceTime).TotalMilliseconds;
			if (elapsedTime <= 0)
				return (null, null);

			double? pDistance = null;
			double? sDistance = null;

			TrTimeTableItem lastItem = null;
			foreach (var item in TimeTable) // P
			{
				if (item.Depth != depth)
					continue;
				if (item.PTime > elapsedTime)
				{
					if (lastItem == null)
						break;
					// 時間での割合を計算
					var magn = (elapsedTime - lastItem.PTime) / (item.PTime - lastItem.PTime);
					pDistance = magn * (item.Distance - lastItem.Distance) + lastItem.Distance;
					break;
				}
				lastItem = item;
			}
			foreach (var item in TimeTable) // S
			{
				if (item.Depth != depth)
					continue;
				if (item.STime > elapsedTime)
				{
					if (lastItem == null)
						break;
					// 時間での割合を計算
					var magn = (elapsedTime - lastItem.STime) / (item.STime - lastItem.STime);
					sDistance = magn * (item.Distance - lastItem.Distance) + lastItem.Distance;
					break;
				}
				lastItem = item;
			}
			return (pDistance, sDistance);
		}

		public void Initalize()
		{
			TimeTable = LZ4MessagePackSerializer.Deserialize<TrTimeTableItem[]>(Resources.tjma2001);
		}
	}
}