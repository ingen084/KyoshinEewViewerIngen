using KyoshinEewViewer.BufrParser.Ixac41;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake.MapLayers;

/// <summary>
/// 推計震度分布図（IXAC41）を SKRuntimeEffect（SKSL）で描画するレイヤー。
/// 表示用画像の事前レンダリングは行わず、計測震度の値そのものを格納した値テクスチャ（LODピラミッド）を
/// 電文受信時に1回構築し、投影変換・震度→色変換は毎フレームピクセル単位でシェーダーが実行する。
/// ズームに応じてメッシュ粒度（1次/2次/3次/4分の1地域メッシュ）を自動切替（LOD）する。
/// </summary>
public class EstimatedIntensityLayer : MapLayer
{
	/// <summary>
	/// パレットテクスチャの要素数（計測震度×10を丸めた値をインデックスとして使用する）
	/// </summary>
	private const int PaletteSize = 128;

	/// <summary>
	/// 表示下限。この値未満の生値（計測震度×10）は透明として扱う
	/// </summary>
	private const int MinVisibleRawIntensity = 25;

	/// <summary>
	/// セルの画面ピクセルサイズがこの値以上になる最も細かいLODを選択する
	/// </summary>
	private const double MinCellPixelSize = 2.0;

	/// <summary>
	/// Miller図法の緯度方向圧縮率。<see cref="MillerProjection"/> の既定値と一致させる
	/// </summary>
	private const float MillerRate = 0.8f;

	/// <summary>
	/// 1次メッシュの緯度方向の大きさ（度）。LODグリッドの原点整列に使用する
	/// </summary>
	private const double PrimaryMeshLatitudeDegrees = 2.0 / 3.0;

	/// <summary>
	/// 1次メッシュの経度方向の大きさ（度）。LODグリッドの原点整列に使用する
	/// </summary>
	private const double PrimaryMeshLongitudeDegrees = 1.0;

	// LOD階層の集約倍率（4分の1地域メッシュ→3次→2次→1次）
	private const int Level0To1Factor = 4;
	private const int Level1To2Factor = 10;
	private const int Level2To3Factor = 3;

	private const string Sksl = """
		uniform float2 uLeftTopPixel;
		uniform float uZoom;
		uniform float uMillerRate;
		uniform float2 uOriginLatLon; // x=緯度, y=経度（グリッド南西端）
		uniform float2 uCellSize;     // x=緯度方向セルサイズ(度), y=経度方向セルサイズ(度)
		uniform float2 uGridSize;     // x=幅(列数), y=高さ(行数)
		uniform float uOpacity;
		uniform shader uDataTex;
		uniform shader uPaletteTex;

		const float PI = 3.1415926535897931;
		const float TILE_SIZE = 256.0;
		const float PIXELS_PER_LON_DEGREE = TILE_SIZE / 360.0;
		const float PIXELS_PER_LON_RADIAN = TILE_SIZE / (2.0 * PI);
		const float ORIGIN_PX = 128.0;

		half4 main(float2 fragCoord) {
			// 画面ローカル座標 → 現在のズームでのワールドピクセル座標
			float2 worldPixel = fragCoord + uLeftTopPixel;
			// ワールドピクセル座標 → ズーム0基準のポイント座標
			float2 pt = worldPixel * exp2(-uZoom);

			// 経度はMiller/Mercatorとも線形
			float lon = (pt.x - ORIGIN_PX) / PIXELS_PER_LON_DEGREE;

			// Millerはメルカトルへ渡す前に緯度をuMillerRate倍しているため、
			// 逆変換ではメルカトル逆関数に通す前にY座標をuMillerRate倍し、
			// 得られた緯度を最後にuMillerRateで割って戻す
			float baseY = pt.y * uMillerRate;
			float latRadians = (ORIGIN_PX - baseY) / PIXELS_PER_LON_RADIAN;
			float latMerc = degrees(2.0 * atan(exp(latRadians)) - PI / 2.0);
			float lat = latMerc / uMillerRate;

			float col = floor((lon - uOriginLatLon.y) / uCellSize.y);
			float rowFromSouth = floor((lat - uOriginLatLon.x) / uCellSize.x);

			if (col < 0.0 || col >= uGridSize.x || rowFromSouth < 0.0 || rowFromSouth >= uGridSize.y)
				return half4(0);

			// 値テクスチャは画像慣習（行0=北）で格納されているため、南基準の行を反転する
			float texRow = uGridSize.y - 1.0 - rowFromSouth;
			half4 rawSample = uDataTex.eval(float2(col + 0.5, texRow + 0.5));
			float value = rawSample.r * 255.0;

			if (value < 0.5)
				return half4(0);

			half4 color = uPaletteTex.eval(float2(value + 0.5, 0.5));
			// uPaletteTex.eval() は既にプリマルチプライド済みの値を返すため、
			// ここで再度アルファを乗算すると二重プリマルチプライになってしまう
			return half4(color.rgb * uOpacity, color.a * uOpacity);
		}
		""";

	private static readonly SKRuntimeEffect? Effect;
	private static readonly string? EffectCompileError;

	static EstimatedIntensityLayer()
	{
		Effect = SKRuntimeEffect.CreateShader(Sksl, out var error);
		EffectCompileError = string.IsNullOrEmpty(error) ? null : error;
	}

	private static bool _compileErrorLogged;

	/// <summary>
	/// LODピラミッドの1階層分。値テクスチャとそのシェーダーを保持する
	/// </summary>
	private sealed class LodLevel(int width, int height, double cellLatitudeDegrees, double cellLongitudeDegrees, SKBitmap bitmap, SKImage image, SKShader shader) : IDisposable
	{
		public int Width { get; } = width;
		public int Height { get; } = height;
		public double CellLatitudeDegrees { get; } = cellLatitudeDegrees;
		public double CellLongitudeDegrees { get; } = cellLongitudeDegrees;
		public SKBitmap Bitmap { get; } = bitmap;
		public SKImage Image { get; } = image;
		public SKShader Shader { get; } = shader;

		public void Dispose()
		{
			Shader.Dispose();
			Image.Dispose();
			Bitmap.Dispose();
		}
	}

	/// <summary>
	/// 1電文分のLODピラミッド一式。<see cref="UpdateData"/> でバックグラウンド構築後にまとめて差し替える
	/// </summary>
	private sealed class GridBundle(LodLevel[] levels, double originLatitude, double originLongitude) : IDisposable
	{
		/// <summary>
		/// LODレベル配列。添字0が最も細かい（4分の1地域メッシュ）、末尾が最も粗い（1次メッシュ）
		/// </summary>
		public LodLevel[] Levels { get; } = levels;
		public double OriginLatitude { get; } = originLatitude;
		public double OriginLongitude { get; } = originLongitude;

		public void Dispose()
		{
			foreach (var level in Levels)
				level.Dispose();
		}
	}

	private readonly Lock _swapLock = new();
	private GridBundle? _gridBundle;
	private readonly List<GridBundle> _pendingBundleDisposal = [];
	private int _buildGeneration;

	private readonly Lock _fallbackLock = new();
	private readonly Dictionary<LodLevel, SKImage> _fallbackImages = [];

	private bool _isVisible;
	/// <summary>
	/// レイヤーを表示するかどうか
	/// </summary>
	public bool IsVisible
	{
		get => _isVisible;
		set
		{
			if (_isVisible == value)
				return;
			_isVisible = value;
			RefreshRequest();
		}
	}

	private double _opacity = 1;
	/// <summary>
	/// 不透明度（0-1）
	/// </summary>
	public double Opacity
	{
		get => _opacity;
		set
		{
			if (_opacity == value)
				return;
			_opacity = value;
			RefreshRequest();
		}
	}

	private EstimatedIntensityColorMode _colorMode = EstimatedIntensityColorMode.IntensityScale;

	public override bool NeedPersistentUpdate => false;

	// パレット（インデックス=計測震度×10、値=色）。SKSLシェーダーへ渡すテクスチャとフォールバック描画の両方で使用する
	private SKColor[]? _paletteColors;
	private SKBitmap? _paletteBitmap;
	private SKImage? _paletteImage;
	private SKShader? _paletteShader;

	/// <summary>
	/// 推計震度分布データを更新する。null を渡すと表示をクリアする
	/// </summary>
	public void UpdateData(EstimatedSeismicIntensityReport? report)
	{
		var generation = Interlocked.Increment(ref _buildGeneration);

		if (report == null || report.Meshes.Count == 0)
		{
			lock (_swapLock)
			{
				if (generation != _buildGeneration)
					return;
				if (_gridBundle != null)
					_pendingBundleDisposal.Add(_gridBundle);
				_gridBundle = null;
			}
			ClearFallbackCache();
			RefreshRequest();
			return;
		}

		Task.Run(() =>
		{
			GridBundle bundle;
			try
			{
				bundle = BuildGridBundle(report);
			}
			catch (Exception ex)
			{
				LogHost.Default.Warn(ex, "推計震度分布図のグリッド構築に失敗しました");
				return;
			}

			lock (_swapLock)
			{
				// 後続のUpdateData呼び出しで世代が進んでいた場合、このビルド結果は使わず破棄する
				if (generation != _buildGeneration)
				{
					bundle.Dispose();
					return;
				}
				if (_gridBundle != null)
					_pendingBundleDisposal.Add(_gridBundle);
				_gridBundle = bundle;
			}
			ClearFallbackCache();
			RefreshRequest();
		});
	}

	/// <summary>
	/// 配色モードを変更する
	/// </summary>
	public void SetColorMode(EstimatedIntensityColorMode mode)
	{
		if (_colorMode == mode)
			return;
		_colorMode = mode;
		BuildPalette();
		RefreshRequest();
	}

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		BuildPalette();
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (!IsVisible)
			return;

		GridBundle? bundle;
		lock (_swapLock)
		{
			foreach (var pending in _pendingBundleDisposal)
				pending.Dispose();
			_pendingBundleDisposal.Clear();
			bundle = _gridBundle;
		}
		if (bundle == null || bundle.Levels.Length == 0 || _paletteShader == null)
			return;

		var level = SelectLevel(bundle.Levels, param.Zoom);

		if (Effect != null)
		{
			RenderWithShader(canvas, param, bundle, level);
			return;
		}

		if (!_compileErrorLogged)
		{
			_compileErrorLogged = true;
			LogHost.Default.Warn($"推計震度分布図シェーダーのコンパイルに失敗したためフォールバック描画を使用します: {EffectCompileError}");
		}
		RenderFallback(canvas, param, bundle, level);
	}

	private void RenderWithShader(SKCanvas canvas, LayerRenderParameter param, GridBundle bundle, LodLevel level)
	{
		using var uniforms = new SKRuntimeEffectUniforms(Effect!)
		{
			["uLeftTopPixel"] = new SKPoint((float)param.LeftTopPixel.X, (float)param.LeftTopPixel.Y),
			["uZoom"] = (float)param.Zoom,
			["uMillerRate"] = MillerRate,
			["uOriginLatLon"] = new SKPoint((float)bundle.OriginLatitude, (float)bundle.OriginLongitude),
			["uCellSize"] = new SKPoint((float)level.CellLatitudeDegrees, (float)level.CellLongitudeDegrees),
			["uGridSize"] = new SKPoint(level.Width, level.Height),
			["uOpacity"] = (float)Opacity,
		};
		using var children = new SKRuntimeEffectChildren(Effect!)
		{
			["uDataTex"] = level.Shader,
			["uPaletteTex"] = _paletteShader,
		};

		using var shader = Effect!.ToShader(uniforms, children);
		using var paint = new SKPaint { Shader = shader };
		canvas.DrawRect(new SKRect(0, 0, (float)param.PixelBound.Width, (float)param.PixelBound.Height), paint);
	}

	private void RenderFallback(SKCanvas canvas, LayerRenderParameter param, GridBundle bundle, LodLevel level)
	{
		var image = GetOrBuildFallbackImage(level);
		if (image == null)
			return;

		// 経度方向は投影が線形なので、画像全体で共通のX範囲を1度だけ計算する
		var projection = new MillerProjection { Rate = MillerRate };
		var leftLon = bundle.OriginLongitude;
		var rightLon = bundle.OriginLongitude + level.Width * level.CellLongitudeDegrees;
		var leftPixelX = new Location(0, (float)leftLon).ToPixel(param.Zoom, projection).X - param.LeftTopPixel.X;
		var rightPixelX = new Location(0, (float)rightLon).ToPixel(param.Zoom, projection).X - param.LeftTopPixel.X;

		// 緯度方向は投影が非線形なので、複数の水平スライスに分けて区分線形近似する
		const int bandCount = 48;
		var rowsPerBand = Math.Max(1, level.Height / bandCount);

		using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Clamp(Opacity * 255, 0, 255)) };
		var sampling = new SKSamplingOptions(SKFilterMode.Nearest);

		for (var bandStart = 0; bandStart < level.Height; bandStart += rowsPerBand)
		{
			var bandEnd = Math.Min(bandStart + rowsPerBand, level.Height);

			// bandStart/bandEndは画像座標（行0=北）。北端・南端の緯度を求める
			var topLatitude = bundle.OriginLatitude + (level.Height - bandStart) * level.CellLatitudeDegrees;
			var bottomLatitude = bundle.OriginLatitude + (level.Height - bandEnd) * level.CellLatitudeDegrees;

			var topPixelY = new Location((float)topLatitude, 0).ToPixel(param.Zoom, projection).Y - param.LeftTopPixel.Y;
			var bottomPixelY = new Location((float)bottomLatitude, 0).ToPixel(param.Zoom, projection).Y - param.LeftTopPixel.Y;

			var srcRect = new SKRect(0, bandStart, level.Width, bandEnd);
			var dstRect = new SKRect((float)leftPixelX, (float)topPixelY, (float)rightPixelX, (float)bottomPixelY);
			canvas.DrawImage(image, srcRect, dstRect, sampling, paint);
		}
	}

	/// <summary>
	/// フォールバック描画用のパレット適用済みRGBAビットマップをLODレベルごとに遅延生成・キャッシュする
	/// </summary>
	private SKImage? GetOrBuildFallbackImage(LodLevel level)
	{
		lock (_fallbackLock)
		{
			if (_fallbackImages.TryGetValue(level, out var cached))
				return cached;

			if (_paletteColors == null)
				return null;

			using var rgba = new SKBitmap(new SKImageInfo(level.Width, level.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
			unsafe
			{
				var srcBase = (byte*)level.Bitmap.GetPixels();
				var srcRowBytes = level.Bitmap.RowBytes;
				for (var y = 0; y < level.Height; y++)
				{
					var srcRow = srcBase + y * srcRowBytes;
					for (var x = 0; x < level.Width; x++)
					{
						var raw = srcRow[x];
						rgba.SetPixel(x, y, raw < MinVisibleRawIntensity ? SKColors.Transparent : _paletteColors[Math.Min((int)raw, PaletteSize - 1)]);
					}
				}
			}

			var image = SKImage.FromBitmap(rgba);
			_fallbackImages[level] = image;
			return image;
		}
	}

	private void ClearFallbackCache()
	{
		lock (_fallbackLock)
		{
			foreach (var image in _fallbackImages.Values)
				image.Dispose();
			_fallbackImages.Clear();
		}
	}

	/// <summary>
	/// 現在のズームで使用するLODレベルを選択する。
	/// 経度方向セルの画面ピクセルサイズが <see cref="MinCellPixelSize"/> 以上になる最も細かいレベルを選ぶ
	/// </summary>
	private static LodLevel SelectLevel(LodLevel[] levels, double zoom)
	{
		foreach (var level in levels)
		{
			var pixelSize = level.CellLongitudeDegrees * (MercatorProjection.TileSize / 360.0) * Math.Pow(2, zoom);
			if (pixelSize >= MinCellPixelSize)
				return level;
		}
		return levels[^1];
	}

	/// <summary>
	/// 電文のメッシュ一覧からLODピラミッド（4分の1地域メッシュ〜1次メッシュの4階層）を構築する。
	/// 呼び出し元でバックグラウンドスレッド実行を想定した重い処理
	/// </summary>
	private static GridBundle BuildGridBundle(EstimatedSeismicIntensityReport report)
	{
		var minLatitude = double.MaxValue;
		var minLongitude = double.MaxValue;
		var maxLatitude = double.MinValue;
		var maxLongitude = double.MinValue;
		foreach (var mesh in report.Meshes)
		{
			var northEastLatitude = mesh.SouthWestLatitude + MeshCodeResolver.CellLatitudeDegrees;
			var northEastLongitude = mesh.SouthWestLongitude + MeshCodeResolver.CellLongitudeDegrees;
			if (mesh.SouthWestLatitude < minLatitude)
				minLatitude = mesh.SouthWestLatitude;
			if (mesh.SouthWestLongitude < minLongitude)
				minLongitude = mesh.SouthWestLongitude;
			if (northEastLatitude > maxLatitude)
				maxLatitude = northEastLatitude;
			if (northEastLongitude > maxLongitude)
				maxLongitude = northEastLongitude;
		}

		// 全LODが単一の原点を共有できるよう、bboxを1次メッシュ境界（緯度2/3度・経度1度の倍数）に整列させる
		var originLatitude = Math.Floor(minLatitude / PrimaryMeshLatitudeDegrees) * PrimaryMeshLatitudeDegrees;
		var originLongitude = Math.Floor(minLongitude / PrimaryMeshLongitudeDegrees) * PrimaryMeshLongitudeDegrees;
		var northEastLatitudeAligned = Math.Ceiling(maxLatitude / PrimaryMeshLatitudeDegrees) * PrimaryMeshLatitudeDegrees;
		var northEastLongitudeAligned = Math.Ceiling(maxLongitude / PrimaryMeshLongitudeDegrees) * PrimaryMeshLongitudeDegrees;

		var level3Width = (int)Math.Round((northEastLongitudeAligned - originLongitude) / PrimaryMeshLongitudeDegrees);
		var level3Height = (int)Math.Round((northEastLatitudeAligned - originLatitude) / PrimaryMeshLatitudeDegrees);

		var level0Width = level3Width * Level2To3Factor * Level1To2Factor * Level0To1Factor;
		var level0Height = level3Height * Level2To3Factor * Level1To2Factor * Level0To1Factor;

		// L0: 4分の1地域メッシュ（原データ）。行0=南（南基準の行インデックス）
		var level0 = new byte[level0Height, level0Width];
		foreach (var mesh in report.Meshes)
		{
			var col = (int)Math.Round((mesh.SouthWestLongitude - originLongitude) / MeshCodeResolver.CellLongitudeDegrees);
			var row = (int)Math.Round((mesh.SouthWestLatitude - originLatitude) / MeshCodeResolver.CellLatitudeDegrees);
			if (col < 0 || col >= level0Width || row < 0 || row >= level0Height)
				continue;

			var raw = (byte)Math.Clamp((int)Math.Round(mesh.CalculatedIntensity * 10), 0, 255);
			// 子メッシュの最大値で集約する（防災上安全側）ため、同一セルへの複数書き込みも最大値を採用する
			if (raw > level0[row, col])
				level0[row, col] = raw;
		}

		var level1 = Aggregate(level0, level0Height, level0Width, Level0To1Factor);
		var level2 = Aggregate(level1, level0Height / Level0To1Factor, level0Width / Level0To1Factor, Level1To2Factor);
		var level3 = Aggregate(level2, level0Height / Level0To1Factor / Level1To2Factor, level0Width / Level0To1Factor / Level1To2Factor, Level2To3Factor);

		var levels = new[]
		{
			CreateLevel(level0, level0Height, level0Width, MeshCodeResolver.CellLatitudeDegrees, MeshCodeResolver.CellLongitudeDegrees),
			CreateLevel(level1, level0Height / Level0To1Factor, level0Width / Level0To1Factor,
				MeshCodeResolver.CellLatitudeDegrees * Level0To1Factor, MeshCodeResolver.CellLongitudeDegrees * Level0To1Factor),
			CreateLevel(level2, level0Height / Level0To1Factor / Level1To2Factor, level0Width / Level0To1Factor / Level1To2Factor,
				MeshCodeResolver.CellLatitudeDegrees * Level0To1Factor * Level1To2Factor, MeshCodeResolver.CellLongitudeDegrees * Level0To1Factor * Level1To2Factor),
			CreateLevel(level3, level3Height, level3Width, PrimaryMeshLatitudeDegrees, PrimaryMeshLongitudeDegrees),
		};

		return new GridBundle(levels, originLatitude, originLongitude);
	}

	/// <summary>
	/// 子メッシュの最大値で1階層粗いグリッドへ集約する
	/// </summary>
	private static byte[,] Aggregate(byte[,] source, int sourceHeight, int sourceWidth, int factor)
	{
		var destHeight = sourceHeight / factor;
		var destWidth = sourceWidth / factor;
		var dest = new byte[destHeight, destWidth];
		for (var dy = 0; dy < destHeight; dy++)
		{
			for (var dx = 0; dx < destWidth; dx++)
			{
				byte max = 0;
				for (var sy = 0; sy < factor; sy++)
				{
					var sourceRow = dy * factor + sy;
					for (var sx = 0; sx < factor; sx++)
					{
						var value = source[sourceRow, dx * factor + sx];
						if (value > max)
							max = value;
					}
				}
				dest[dy, dx] = max;
			}
		}
		return dest;
	}

	/// <summary>
	/// 南基準の値グリッドからGray8の値テクスチャ（画像慣習: 行0=北）を構築し、Nearestサンプリングのシェーダーにする
	/// </summary>
	private static LodLevel CreateLevel(byte[,] grid, int height, int width, double cellLatitudeDegrees, double cellLongitudeDegrees)
	{
		var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
		unsafe
		{
			var basePtr = (byte*)bitmap.GetPixels();
			var rowBytes = bitmap.RowBytes;
			for (var southRow = 0; southRow < height; southRow++)
			{
				// テクスチャは画像慣習(行0=北)なので、南基準の行インデックスを反転して書き込む
				var destRow = height - 1 - southRow;
				var destPtr = basePtr + destRow * rowBytes;
				for (var col = 0; col < width; col++)
					destPtr[col] = grid[southRow, col];
			}
		}

		var image = SKImage.FromBitmap(bitmap);
		var shader = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Nearest));
		return new LodLevel(width, height, cellLatitudeDegrees, cellLongitudeDegrees, bitmap, image, shader);
	}

	/// <summary>
	/// 現在の配色モードでパレット（128要素、インデックス=計測震度×10）を再構築する。
	/// テーマ変更時（<see cref="RefreshResourceCache"/>）と配色モード変更時（<see cref="SetColorMode"/>）に呼ばれる
	/// </summary>
	private void BuildPalette()
	{
		var colors = new SKColor[PaletteSize];
		for (var i = 0; i < PaletteSize; i++)
			colors[i] = i < MinVisibleRawIntensity ? SKColors.Transparent : ResolveColor(i);

		var bitmap = new SKBitmap(new SKImageInfo(PaletteSize, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
		for (var i = 0; i < PaletteSize; i++)
			bitmap.SetPixel(i, 0, colors[i]);
		var image = SKImage.FromBitmap(bitmap);
		var shader = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Nearest));

		var oldBitmap = _paletteBitmap;
		var oldImage = _paletteImage;
		var oldShader = _paletteShader;

		_paletteColors = colors;
		_paletteBitmap = bitmap;
		_paletteImage = image;
		_paletteShader = shader;

		oldShader?.Dispose();
		oldImage?.Dispose();
		oldBitmap?.Dispose();

		ClearFallbackCache();
	}

	private SKColor ResolveColor(int rawIndex)
	{
		var value = rawIndex / 10f;
		return _colorMode switch
		{
			EstimatedIntensityColorMode.ContinuousGradient => ResolveGradientColor(value),
			_ => GetIntensityColor(ClassifyIntensity(value)),
		};
	}

	/// <summary>
	/// 計測震度を震度階級（<see cref="JmaIntensity.Int3"/>〜<see cref="JmaIntensity.Int7"/>）へ量子化する
	/// </summary>
	private static JmaIntensity ClassifyIntensity(float value)
		=> value switch
		{
			< 3.5f => JmaIntensity.Int3,
			< 4.5f => JmaIntensity.Int4,
			< 5.0f => JmaIntensity.Int5Lower,
			< 5.5f => JmaIntensity.Int5Upper,
			< 6.0f => JmaIntensity.Int6Lower,
			< 6.5f => JmaIntensity.Int6Upper,
			_ => JmaIntensity.Int7,
		};

	// 各震度階級の下限値と、グラデーションモードで使用するアンカー値
	private static readonly (float Value, JmaIntensity Intensity)[] GradientAnchors =
	[
		(2.5f, JmaIntensity.Int3),
		(3.5f, JmaIntensity.Int4),
		(4.5f, JmaIntensity.Int5Lower),
		(5.0f, JmaIntensity.Int5Upper),
		(5.5f, JmaIntensity.Int6Lower),
		(6.0f, JmaIntensity.Int6Upper),
		(6.5f, JmaIntensity.Int7),
	];

	/// <summary>
	/// 各震度階級の境界にその階級色のアンカーを置き、RGBを線形補間する
	/// </summary>
	private SKColor ResolveGradientColor(float value)
	{
		if (value <= GradientAnchors[0].Value)
			return GetIntensityColor(GradientAnchors[0].Intensity);
		if (value >= GradientAnchors[^1].Value)
			return GetIntensityColor(GradientAnchors[^1].Intensity);

		for (var i = 1; i < GradientAnchors.Length; i++)
		{
			var (upperValue, upperIntensity) = GradientAnchors[i];
			if (value > upperValue)
				continue;

			var (lowerValue, lowerIntensity) = GradientAnchors[i - 1];
			var lowerColor = GetIntensityColor(lowerIntensity);
			var upperColor = GetIntensityColor(upperIntensity);
			var t = (value - lowerValue) / (upperValue - lowerValue);

			return new SKColor(
				(byte)(lowerColor.Red + (upperColor.Red - lowerColor.Red) * t),
				(byte)(lowerColor.Green + (upperColor.Green - lowerColor.Green) * t),
				(byte)(lowerColor.Blue + (upperColor.Blue - lowerColor.Blue) * t),
				255);
		}

		return GetIntensityColor(GradientAnchors[^1].Intensity);
	}

	// IntensityPaintCache未初期化時（起動直後など）に使用するフォールバック色。JMA震度階級の慣用色に準拠する
	private static readonly Dictionary<JmaIntensity, SKColor> FallbackColors = new()
	{
		[JmaIntensity.Int3] = new SKColor(0xF9, 0xE7, 0x00),
		[JmaIntensity.Int4] = new SKColor(0xF9, 0xA2, 0x00),
		[JmaIntensity.Int5Lower] = new SKColor(0xF9, 0x64, 0x00),
		[JmaIntensity.Int5Upper] = new SKColor(0xE5, 0x00, 0x00),
		[JmaIntensity.Int6Lower] = new SKColor(0xC0, 0x00, 0x00),
		[JmaIntensity.Int6Upper] = new SKColor(0xA0, 0x00, 0x00),
		[JmaIntensity.Int7] = new SKColor(0x60, 0x00, 0x60),
	};

	private static SKColor GetIntensityColor(JmaIntensity intensity)
	{
		if (FixedObjectRenderer.PaintCacheInitialized && FixedObjectRenderer.IntensityPaintCache.TryGetValue(intensity, out var paints))
			return paints.Background.Color;

		return FallbackColors.TryGetValue(intensity, out var fallback) ? fallback : SKColors.Gray;
	}
}
