using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
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

	/// <summary>
	/// ベースレイヤー
	/// 境界線よりも優先度が低い
	/// </summary>
	[Reactive]
	public MapLayer[]? BaseLayers { get; protected set; }

	/// <summary>
	/// オーバーレイレイヤー
	/// 境界線よりも優先度が高い
	/// </summary>
	[Reactive]
	public MapLayer[]? OverlayLayers { get; protected set; }

	/// <summary>
	/// 地図に着色する内容のマップ
	/// </summary>
	[Reactive]
	public Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap { get; protected set; }

	/// <summary>
	/// 現在この Series が表示させたい地図上での範囲
	/// </summary>
	[Reactive]
	public Rect? FocusBound { get; set; }

	/// <summary>
	/// タブ内部に表示させるコントロール
	/// </summary>
	public abstract Control DisplayControl { get; }

	[Reactive]
	public Thickness MapPadding { get; protected set; }

	public abstract void Activating();
	public abstract void Deactivated();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}
