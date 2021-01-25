using KyoshinEewViewer.MapControl.Projections;
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

		public TimeSpan TimeOffset { get; protected internal set; }

		public abstract void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection);
		protected internal abstract void OnTick();
	}
}
