using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

public class MiniMapPathConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && 
            values[0] is double width && width > 0 &&
            values[1] is double height && height > 0)
        {
            var cornerRadius = 8.0;
            var largeCornerRadius = Math.Min(150, Math.Min(width * 0.6, height * 0.5));
            var cutoffY = height - largeCornerRadius - 0.5;
            var cutoffX = width - largeCornerRadius - 0.5;

            // 45度の斜めカットのための計算
            // 丸角の中心から45度方向への接線を計算
            var offset = cornerRadius * (Math.Sqrt(2) - 0.75);

            var segments = new PathSegments
			{
				// 上辺
				new LineSegment() { Point = new Avalonia.Point(width - cornerRadius, 0.5) },
				// 右上角
				new ArcSegment()
                {
                    Point = new Avalonia.Point(width - 0.5, cornerRadius),
                    Size = new Avalonia.Size(cornerRadius, cornerRadius),
                    RotationAngle = 0,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise
                },
				// 右辺（斜めカット開始位置まで）
				new LineSegment() { Point = new Avalonia.Point(width - 0.5, cutoffY + offset) },
				// 右下の斜めカット部分の上側の丸角
				new ArcSegment()
                {
                    Point = new Avalonia.Point(width - 0.5 - cornerRadius + offset, cutoffY + cornerRadius + offset),
                    Size = new Avalonia.Size(cornerRadius, cornerRadius),
                    RotationAngle = 0,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise,
                },
				// 斜めカット
				new LineSegment() { Point = new Avalonia.Point(cutoffX + cornerRadius - offset, height - 0.5 - cornerRadius + offset) },
				// 右下の斜めカット部分の下側の丸角
				new ArcSegment()
                {
                    Point = new Avalonia.Point(cutoffX - offset, height - 0.5),
                    Size = new Avalonia.Size(cornerRadius, cornerRadius),
                    RotationAngle = 0,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise,
                },
				// 下辺
				new LineSegment() { Point = new Avalonia.Point(cornerRadius, height - 0.5) },
				// 左下角
				new ArcSegment()
                {
                    Point = new Avalonia.Point(0.5, height - cornerRadius),
                    Size = new Avalonia.Size(cornerRadius, cornerRadius),
                    RotationAngle = 0,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise
                },
				// 左辺
				new LineSegment() { Point = new Avalonia.Point(0.5, cornerRadius) },
				// 左上角
				new ArcSegment()
                {
                    Point = new Avalonia.Point(cornerRadius, 0.5),
                    Size = new Avalonia.Size(cornerRadius, cornerRadius),
                    RotationAngle = 0,
                    IsLargeArc = false,
                    SweepDirection = SweepDirection.Clockwise
                },
            };

            var figure = new PathFigure
            {
                StartPoint = new Avalonia.Point(cornerRadius, 0.5),
                IsClosed = true,
                Segments = segments,
            };
            return new PathGeometry()
            {
                Figures = [figure],
            };
        }

        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
