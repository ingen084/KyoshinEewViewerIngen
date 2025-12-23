using KyoshinEewViewer.Core.Models;
using System;

namespace KyoshinEewViewer.Map.Layers;

public struct LayerRenderParameter : IEquatable<LayerRenderParameter>
{
	public double Zoom { get; set; }
	public PointD LeftTopLocation { get; set; }
	public PointD LeftTopPixel { get; set; }
	public RectD PixelBound { get; set; }
	public RectD ViewAreaRect { get; set; }
	public Avalonia.Thickness Padding { get; set; }

	private const double Tolerance = 0.0001;

	public readonly bool Equals(LayerRenderParameter other)
		=> Math.Abs(Zoom - other.Zoom) < Tolerance &&
		   LeftTopLocation.Equals(other.LeftTopLocation) &&
		   LeftTopPixel.Equals(other.LeftTopPixel) &&
		   PixelBound.Equals(other.PixelBound) &&
		   ViewAreaRect.Equals(other.ViewAreaRect) &&
		   Padding.Equals(other.Padding);

	public override readonly bool Equals(object? obj)
		=> obj is LayerRenderParameter other && Equals(other);

	public override readonly int GetHashCode()
		=> HashCode.Combine(Zoom, LeftTopLocation, LeftTopPixel, PixelBound, ViewAreaRect, Padding);

	public static bool operator ==(LayerRenderParameter left, LayerRenderParameter right)
		=> left.Equals(right);

	public static bool operator !=(LayerRenderParameter left, LayerRenderParameter right)
		=> !left.Equals(right);
}
