using KyoshinEewViewer.Map.Projections;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map
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

		public abstract void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isDarkTheme, MapProjection projection);
		protected internal abstract void OnTick();
	}
}
