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

		public TimeSpan TimeOffset { get; internal protected set; }

		public abstract void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme, MapProjection projection);
		internal protected abstract void OnTick();
	}
}
