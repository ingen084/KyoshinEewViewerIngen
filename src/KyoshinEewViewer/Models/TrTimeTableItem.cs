using MessagePack;

namespace KyoshinEewViewer.Models
{
	[MessagePackObject]
	public class TrTimeTableItem
	{
		public TrTimeTableItem(int pTime, int sTime, int depth, int distance)
		{
			PTime = pTime;
			STime = sTime;
			Depth = depth;
			Distance = distance;
		}

		[Key(0)]
		public int PTime { get; }

		[Key(1)]
		public int STime { get; }

		[Key(2)]
		public int Depth { get; }

		[Key(3)]
		public int Distance { get; }
	}
}