using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl.RenderObjects
{
	public class EllipseRenderObject : RenderObject
	{
		private bool NeedUpdateGeometry { get; set; }

		private Location center;

		/// <summary>
		/// 中心(緯度経度)
		/// </summary>
		public Location Center
		{
			get => center;
			set
			{
				center = value;
				NeedUpdateGeometry = true;
			}
		}

		/// <summary>
		/// 中心座標からのオフセット(メートル)
		/// </summary>
		public Point Offset { get; set; }

		private double radius;

		/// <summary>
		/// 半径(メートル)
		/// </summary>
		public double Radius
		{
			get => radius;
			set
			{
				radius = value;
				NeedUpdateGeometry = true;
			}
		}

		private Brush fillBrush;

		/// <summary>
		/// 塗りつぶしブラシ
		/// </summary>
		public Brush FillBrush
		{
			get => fillBrush;
			set
			{
				fillBrush = value;
				fillBrush.Freeze();
			}
		}

		public Pen strokePen;

		/// <summary>
		/// 縁のペン
		/// </summary>
		public Pen StrokePen
		{
			get => strokePen;
			set
			{
				strokePen = value;
				strokePen.Freeze();
			}
		}

		private Geometry GeometryCache { get; set; }
		private double CachedZoom { get; set; }

		// Author: M-nohira
		public void MakeCircleGeometry(double zoom)
		{
			if (Radius <= 0 || Center == null)
			{
				GeometryCache = null;
				return;
			}
			if (CachedZoom == zoom)
				return;
			CachedZoom = zoom;

			const double EATRH_RADIUS = 6378.137;

			var div = 90;
			var pathFigure = new PathFigure
			{
				Segments = new PathSegmentCollection()
			};

			var d_rad = 2 * Math.PI / div;
			var c_lat_rad = (Center.Latitude / 180) * Math.PI;

			var gamma_rad = (Radius / 1000) / EATRH_RADIUS;
			var invert_c_lat_rad = (Math.PI / 2) - c_lat_rad;

			var cos_invert_c_rad = Math.Cos(invert_c_lat_rad);
			var cos_gamma_rad = Math.Cos(gamma_rad);
			var sin_invert_c_rad = Math.Sin(invert_c_lat_rad);
			var sin_gamma_rad = Math.Sin(gamma_rad);

			for (int count = 0; count <= div; count++)
			{
				//球面三角形における正弦余弦定理使用
				var rad = d_rad * count;
				var cos_inv_dist_lat = (cos_invert_c_rad * cos_gamma_rad) + (sin_invert_c_rad * sin_gamma_rad * Math.Cos(rad));
				var sin_d_lon = sin_gamma_rad * Math.Sin(rad) / Math.Sin(Math.Acos(cos_inv_dist_lat));

				var lat = ((Math.PI / 2) - Math.Acos(cos_inv_dist_lat)) * 180 / Math.PI;
				var lon = Center.Longitude + Math.Asin(sin_d_lon) * 180 / Math.PI;
				var loc = new Location((float)lat, (float)lon);

				if (count == 0)
					pathFigure.StartPoint = loc.ToPixel(zoom);
				else
				{
					var segment = new LineSegment(loc.ToPixel(zoom), true);
					segment.Freeze();
					pathFigure.Segments.Add(segment);
				}
			}

			pathFigure.Segments.Freeze();
			pathFigure.Freeze();
			var pathFigures = new PathFigureCollection
			{
				pathFigure
			};
			pathFigures.Freeze();
			var geometry = new PathGeometry(pathFigures);
			GeometryCache = geometry;
			NeedUpdateGeometry = false;
		}

		//TODO: EEWのたびにBrush初期化させるのはまずくないか…？
		public EllipseRenderObject(Dispatcher dispatcher, Location center, double radius, Brush fillBrush = null, Pen strokePen = null, Point? offset = null) : base(dispatcher)
		{
			Center = center ?? throw new ArgumentNullException(nameof(center));
			Offset = offset ?? new Point();
			Radius = radius;
			FillBrush = fillBrush ?? Brushes.Transparent;
			StrokePen = strokePen ?? new Pen(Brushes.Magenta, 3);
			NeedUpdateGeometry = true;
		}

		public override void Render(DrawingContext context, double zoom, Point leftTopLocation)
		{
			MakeCircleGeometry(zoom);
			if (GeometryCache == null)
				return;
			GeometryCache.Transform = new TranslateTransform(leftTopLocation.X, leftTopLocation.Y);
			context.DrawGeometry(FillBrush, StrokePen, GeometryCache);
		}
	}
}