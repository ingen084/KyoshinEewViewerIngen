using KyoshinEewViewer.RenderObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyoshinEewViewer.Controls
{
	/// <summary>
	/// JapanMap.xaml の相互作用ロジック
	/// </summary>
	public partial class JapanMap : UserControl
	{
		public IEnumerable<RenderObject> RenderObjects
		{
			get => (IEnumerable<RenderObject>)GetValue(RenderObjectsProperty);
			set => SetValue(RenderObjectsProperty, value);
		}

		public static readonly DependencyProperty RenderObjectsProperty =
			DependencyProperty.Register("RenderObjects", typeof(IEnumerable<RenderObject>), typeof(JapanMap), new PropertyMetadata(null, (s, e) =>
			{
				if (!(s is JapanMap map))
					return;
				map.overlay.RenderObjects = map.RenderObjects;
				map.overlay.InvalidateVisual();
			}));

		public DateTime LastUpdatedTime
		{
			get => (DateTime)GetValue(LastUpdatedTimeProperty);
			set => SetValue(LastUpdatedTimeProperty, value);
		}

		public static readonly DependencyProperty LastUpdatedTimeProperty =
			DependencyProperty.Register("LastUpdatedTime", typeof(DateTime), typeof(JapanMap), new PropertyMetadata(DateTime.Now, (s, e) =>
			{
				if (!(s is JapanMap map) || Application.Current.MainWindow.WindowState == WindowState.Minimized || !Application.Current.MainWindow.IsVisible)
					return;
				map.overlay.InvalidateVisual();
			}));

		public JapanMap()
		{
			InitializeComponent();
		}
	}

	public class MapOverlay : UIElement
	{
		public IEnumerable<RenderObject> RenderObjects { get; set; }

		/*readonly DrawingGroup backingStore = new DrawingGroup();
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			Render();
			drawingContext.DrawDrawing(backingStore);
		}
		public void Render()
		{
			var drawingContext = backingStore.Open();
			Render(drawingContext);
			drawingContext.Close();
		}*/

		private void Render(DrawingContext drawingContext)
		{
			if (RenderObjects == null)
				return;
			lock (RenderObjects)
			{
				if (!RenderObjects.Any())
					return;
				foreach (var obj in RenderObjects)
					obj.Render(drawingContext);
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			Render(drawingContext);
		}
	}
}