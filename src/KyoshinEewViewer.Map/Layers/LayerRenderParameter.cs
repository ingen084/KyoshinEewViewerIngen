namespace KyoshinEewViewer.Map.Layers;
public struct LayerRenderParameter
{
	public double Zoom { get; set; }
	public PointD LeftTopLocation { get; set; }
	public PointD LeftTopPixel { get; set; }
	public RectD PixelBound { get; set; }
	public RectD ViewAreaRect { get; set; }
	public Avalonia.Thickness Padding { get; set; }
}
