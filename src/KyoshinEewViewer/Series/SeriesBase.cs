using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers.ImageTile;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series;

public abstract class SeriesBase : ReactiveObject, IDisposable
{
	protected SeriesBase(string name)
	{
		Name = name;
	}

	public string Name { get; }
	[Reactive]
	public bool IsEnabled { get; protected set; }

	[Reactive]
	public Thickness MapPadding { get; protected set; }
	[Reactive]
	public ImageTileProvider[]? ImageTileProviders { get; protected set; }
	[Reactive]
	public IRenderObject[]? RenderObjects { get; protected set; }
	[Reactive]
	public RealtimeRenderObject[]? RealtimeRenderObjects { get; protected set; }
	[Reactive]
	public Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap { get; protected set; }

	[Reactive]
	public Rect? FocusBound { get; set; }

	public abstract Control DisplayControl { get; }

	public abstract void Activating();
	public abstract void Deactivated();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}
