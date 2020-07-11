using System;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public abstract class RealtimeRenderObject : IRenderObject
	{
		private DateTime baseTime;
		public DateTime BaseTime
		{
			get => baseTime;
			set
			{
				if (baseTime == value)
					return;
				TimeOffset = TimeSpan.Zero;
				baseTime = value;
			}
		}

		protected internal TimeSpan TimeOffset { get; set; }

		public abstract void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme);
		protected internal abstract void OnTick();
	}
}
