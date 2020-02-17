using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl
{
	public class FeatureCacheController
	{
		private Dispatcher Dispatcher { get; }
		public FeatureCacheController(Dispatcher dispatcher)
		{
			Dispatcher = dispatcher;
		}

		public void SetViewport(Rect rect)
		{

		}
	}
}
