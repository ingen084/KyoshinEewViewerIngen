using Avalonia;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series;

/// <summary>
/// 
/// </summary>
/// <param name="BackgroundLayers">バックグラウンドレイヤー<br/>地形よりも優先度が低い</param>
/// <param name="BaseLayers">ベースレイヤー<br/>境界線よりも優先度が低い</param>
/// <param name="OverlayLayers">オーバーレイレイヤー<br/>境界線よりも優先度が高い</param>
/// <param name="CustomColorMap">地図に着色する内容のマップ</param>
/// <param name="Padding">地図の Padding</param>
/// <param name="LayerSets"> 表示する地図のレイヤーの定義</param>
/// <param name="BorderEmphasis">境界線を強調表示するか</param>
public record struct MapDisplayParameter(
	MapLayer[]? BackgroundLayers,
	MapLayer[]? BaseLayers,
	MapLayer[]? OverlayLayers,
	Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap,
	Thickness Padding,
	LandLayerSet[]? LayerSets,
	bool BorderEmphasis
);
