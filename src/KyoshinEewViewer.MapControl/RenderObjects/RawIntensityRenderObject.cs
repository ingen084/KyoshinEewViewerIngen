using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.RenderObjects
{
	public class RawIntensityRenderObject : RenderObject
	{
		private static Dictionary<float, SolidColorBrush> ColorTable { get; set; }
		private static Typeface TypeFace { get; } = new Typeface(new FontFamily("Yu Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

		public string Name { get; }
		public FormattedText NameFormattedText { get; }
		public RawIntensityRenderObject(Location location, string name, float rawIntensity = float.NaN)
		{
			Location = location ?? throw new ArgumentNullException(nameof(location));
			RawIntensity = rawIntensity;
			Name = name;
			CreateColorMap();
		}

		private void CreateColorMap()
		{
			if (ColorTable != null)
				return;
			//if (Dispatcher.CheckAccess())
			//{
			//	Dispatcher.Invoke(CreateColorMap);
			//	return;
			//}
			ColorTable = new Dictionary<float, SolidColorBrush>()
			{
				{-3f, new SolidColorBrush(Color.FromArgb(255, 0, 0, 205))},
				{-2.9f, new SolidColorBrush(Color.FromArgb(255, 0, 7, 209))},
				{-2.8f, new SolidColorBrush(Color.FromArgb(255, 0, 14, 214))},
				{-2.7f, new SolidColorBrush(Color.FromArgb(255, 0, 21, 218))},
				{-2.6f, new SolidColorBrush(Color.FromArgb(255, 0, 28, 223))},
				{-2.5f, new SolidColorBrush(Color.FromArgb(255, 0, 36, 227))},
				{-2.4f, new SolidColorBrush(Color.FromArgb(255, 0, 43, 231))},
				{-2.3f, new SolidColorBrush(Color.FromArgb(255, 0, 50, 236))},
				{-2.2f, new SolidColorBrush(Color.FromArgb(255, 0, 57, 240))},
				{-2.1f, new SolidColorBrush(Color.FromArgb(255, 0, 64, 245))},
				{-2f, new SolidColorBrush(Color.FromArgb(255, 0, 72, 250))},
				{-1.9f, new SolidColorBrush(Color.FromArgb(255, 0, 85, 238))},
				{-1.8f, new SolidColorBrush(Color.FromArgb(255, 0, 99, 227))},
				{-1.7f, new SolidColorBrush(Color.FromArgb(255, 0, 112, 216))},
				{-1.6f, new SolidColorBrush(Color.FromArgb(255, 0, 126, 205))},
				{-1.5f, new SolidColorBrush(Color.FromArgb(255, 0, 140, 194))},
				{-1.4f, new SolidColorBrush(Color.FromArgb(255, 0, 153, 183))},
				{-1.3f, new SolidColorBrush(Color.FromArgb(255, 0, 167, 172))},
				{-1.2f, new SolidColorBrush(Color.FromArgb(255, 0, 180, 161))},
				{-1.1f, new SolidColorBrush(Color.FromArgb(255, 0, 194, 150))},
				{-1f, new SolidColorBrush(Color.FromArgb(255, 0, 208, 139))},
				{-0.9f, new SolidColorBrush(Color.FromArgb(255, 6, 212, 130))},
				{-0.8f, new SolidColorBrush(Color.FromArgb(255, 12, 216, 121))},
				{-0.7f, new SolidColorBrush(Color.FromArgb(255, 18, 220, 113))},
				{-0.6f, new SolidColorBrush(Color.FromArgb(255, 25, 224, 104))},
				{-0.5f, new SolidColorBrush(Color.FromArgb(255, 31, 228, 96))},
				{-0.4f, new SolidColorBrush(Color.FromArgb(255, 37, 233, 88))},
				{-0.3f, new SolidColorBrush(Color.FromArgb(255, 44, 237, 79))},
				{-0.2f, new SolidColorBrush(Color.FromArgb(255, 50, 241, 71))},
				{-0.1f, new SolidColorBrush(Color.FromArgb(255, 56, 245, 62))},
				{0f, new SolidColorBrush(Color.FromArgb(255, 63, 250, 54))},
				{0.1f, new SolidColorBrush(Color.FromArgb(255, 75, 250, 49))},
				{0.2f, new SolidColorBrush(Color.FromArgb(255, 88, 250, 45))},
				{0.3f, new SolidColorBrush(Color.FromArgb(255, 100, 251, 41))},
				{0.4f, new SolidColorBrush(Color.FromArgb(255, 113, 251, 37))},
				{0.5f, new SolidColorBrush(Color.FromArgb(255, 125, 252, 33))},
				{0.6f, new SolidColorBrush(Color.FromArgb(255, 138, 252, 28))},
				{0.7f, new SolidColorBrush(Color.FromArgb(255, 151, 253, 24))},
				{0.8f, new SolidColorBrush(Color.FromArgb(255, 163, 253, 20))},
				{0.9f, new SolidColorBrush(Color.FromArgb(255, 176, 254, 16))},
				{1f, new SolidColorBrush(Color.FromArgb(255, 189, 255, 12))},
				{1.1f, new SolidColorBrush(Color.FromArgb(255, 195, 254, 10))},
				{1.2f, new SolidColorBrush(Color.FromArgb(255, 202, 254, 9))},
				{1.3f, new SolidColorBrush(Color.FromArgb(255, 208, 254, 8))},
				{1.4f, new SolidColorBrush(Color.FromArgb(255, 215, 254, 7))},
				{1.5f, new SolidColorBrush(Color.FromArgb(255, 222, 255, 5))},
				{1.6f, new SolidColorBrush(Color.FromArgb(255, 228, 254, 4))},
				{1.7f, new SolidColorBrush(Color.FromArgb(255, 235, 255, 3))},
				{1.8f, new SolidColorBrush(Color.FromArgb(255, 241, 254, 2))},
				{1.9f, new SolidColorBrush(Color.FromArgb(255, 248, 255, 1))},
				{2f, new SolidColorBrush(Color.FromArgb(255, 255, 255, 0))},
				{2.1f, new SolidColorBrush(Color.FromArgb(255, 254, 251, 0))},
				{2.2f, new SolidColorBrush(Color.FromArgb(255, 254, 248, 0))},
				{2.3f, new SolidColorBrush(Color.FromArgb(255, 254, 244, 0))},
				{2.4f, new SolidColorBrush(Color.FromArgb(255, 254, 241, 0))},
				{2.5f, new SolidColorBrush(Color.FromArgb(255, 255, 238, 0))},
				{2.6f, new SolidColorBrush(Color.FromArgb(255, 254, 234, 0))},
				{2.7f, new SolidColorBrush(Color.FromArgb(255, 255, 231, 0))},
				{2.8f, new SolidColorBrush(Color.FromArgb(255, 254, 227, 0))},
				{2.9f, new SolidColorBrush(Color.FromArgb(255, 255, 224, 0))},
				{3f, new SolidColorBrush(Color.FromArgb(255, 255, 221, 0))},
				{3.1f, new SolidColorBrush(Color.FromArgb(255, 254, 213, 0))},
				{3.2f, new SolidColorBrush(Color.FromArgb(255, 254, 205, 0))},
				{3.3f, new SolidColorBrush(Color.FromArgb(255, 254, 197, 0))},
				{3.4f, new SolidColorBrush(Color.FromArgb(255, 254, 190, 0))},
				{3.5f, new SolidColorBrush(Color.FromArgb(255, 255, 182, 0))},
				{3.6f, new SolidColorBrush(Color.FromArgb(255, 254, 174, 0))},
				{3.7f, new SolidColorBrush(Color.FromArgb(255, 255, 167, 0))},
				{3.8f, new SolidColorBrush(Color.FromArgb(255, 254, 159, 0))},
				{3.9f, new SolidColorBrush(Color.FromArgb(255, 255, 151, 0))},
				{4f, new SolidColorBrush(Color.FromArgb(255, 255, 144, 0))},
				{4.1f, new SolidColorBrush(Color.FromArgb(255, 254, 136, 0))},
				{4.2f, new SolidColorBrush(Color.FromArgb(255, 254, 128, 0))},
				{4.3f, new SolidColorBrush(Color.FromArgb(255, 254, 121, 0))},
				{4.4f, new SolidColorBrush(Color.FromArgb(255, 254, 113, 0))},
				{4.5f, new SolidColorBrush(Color.FromArgb(255, 255, 106, 0))},
				{4.6f, new SolidColorBrush(Color.FromArgb(255, 254, 98, 0))},
				{4.7f, new SolidColorBrush(Color.FromArgb(255, 255, 90, 0))},
				{4.8f, new SolidColorBrush(Color.FromArgb(255, 254, 83, 0))},
				{4.9f, new SolidColorBrush(Color.FromArgb(255, 255, 75, 0))},
				{5f, new SolidColorBrush(Color.FromArgb(255, 255, 68, 0))},
				{5.1f, new SolidColorBrush(Color.FromArgb(255, 254, 61, 0))},
				{5.2f, new SolidColorBrush(Color.FromArgb(255, 253, 54, 0))},
				{5.3f, new SolidColorBrush(Color.FromArgb(255, 252, 47, 0))},
				{5.4f, new SolidColorBrush(Color.FromArgb(255, 251, 40, 0))},
				{5.5f, new SolidColorBrush(Color.FromArgb(255, 250, 33, 0))},
				{5.6f, new SolidColorBrush(Color.FromArgb(255, 249, 27, 0))},
				{5.7f, new SolidColorBrush(Color.FromArgb(255, 248, 20, 0))},
				{5.8f, new SolidColorBrush(Color.FromArgb(255, 247, 13, 0))},
				{5.9f, new SolidColorBrush(Color.FromArgb(255, 246, 6, 0))},
				{6f, new SolidColorBrush(Color.FromArgb(255, 245, 0, 0))},
				{6.1f, new SolidColorBrush(Color.FromArgb(255, 238, 0, 0))},
				{6.2f, new SolidColorBrush(Color.FromArgb(255, 230, 0, 0))},
				{6.3f, new SolidColorBrush(Color.FromArgb(255, 223, 0, 0))},
				{6.4f, new SolidColorBrush(Color.FromArgb(255, 215, 0, 0))},
				{6.5f, new SolidColorBrush(Color.FromArgb(255, 208, 0, 0))},
				{6.6f, new SolidColorBrush(Color.FromArgb(255, 200, 0, 0))},
				{6.7f, new SolidColorBrush(Color.FromArgb(255, 192, 0, 0))},
				{6.8f, new SolidColorBrush(Color.FromArgb(255, 185, 0, 0))},
				{6.9f, new SolidColorBrush(Color.FromArgb(255, 177, 0, 0))},
				{7.0f, new SolidColorBrush(Color.FromArgb(255, 170, 0, 0))},
			};
			foreach (var value in ColorTable.Values)
				value.Freeze();
		}

		/// <summary>
		/// 地理座標
		/// </summary>
		public Location Location { get; set; }

		/// <summary>
		/// 生の震度の値
		/// </summary>
		public float RawIntensity { get; set; }

		public override void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			var intensity = (float)Math.Min(Math.Max(RawIntensity, -3), 7.0);
			if (float.IsNaN(intensity))
				return;
			var circleSize = (zoom - 4) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(zoom);
			if (!bound.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			context.DrawEllipse(ColorTable[intensity], null, pointCenter - (Vector)leftTopPixel, circleSize, circleSize);
			if (zoom >= 9)
			{
				var text = new FormattedText(zoom >= 9.5 ? (Name + "\n" + intensity.ToString("0.0")) : Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TypeFace, 14, isDarkTheme ? Brushes.White : Brushes.Black, 94)
				{
					LineHeight = circleSize * 1.2
				};
				context.DrawText(text, pointCenter - (Vector)leftTopPixel + new Vector(circleSize * 1.5, -circleSize));
			}
		}
	}
}