using KyoshinMonitorLib;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl.RenderObjects
{
	public abstract class RenderObject
	{
		protected Dispatcher Dispatcher { get; }

		public RenderObject(Dispatcher mainDispatcher)
		{
			Dispatcher = mainDispatcher;
		}

		public abstract void Render(DrawingContext context, double zoom, Point leftTopLocation);
	}
}