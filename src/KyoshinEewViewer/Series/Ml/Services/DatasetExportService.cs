using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.Series.Ml.Models;
using KyoshinMonitorLib.SkiaImages;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Ml.Services;

/// <summary>
/// 機械学習用データセットをエクスポートするサービス
/// </summary>
public class DatasetExportService
{
	/// <summary>
	/// エクスポートの進捗情報
	/// </summary>
	public class ExportProgress
	{
		public int CurrentFrame { get; set; }
		public int TotalFrames { get; set; }
		public string Message { get; set; } = "";
		public double Percentage => TotalFrames > 0 ? (double)CurrentFrame / TotalFrames * 100 : 0;
	}

	/// <summary>
	/// データセットをエクスポートする
	/// </summary>
	/// <param name="outputDir">出力ディレクトリ</param>
	/// <param name="replayData">リプレイデータ</param>
	/// <param name="observationPoints">観測点リスト</param>
	/// <param name="earthquakes">地震情報リスト</param>
	/// <param name="progress">進捗報告</param>
	/// <param name="cancellationToken">キャンセルトークン</param>
	public async Task ExportAsync(
		string outputDir,
		KyoshinMonitorImageReplayData[] replayData,
		RealtimeObservationPoint[] observationPoints,
		ShindoDbEarthquake[] earthquakes,
		IProgress<ExportProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		if (replayData.Length == 0)
			throw new InvalidOperationException("リプレイデータが空です");

		if (observationPoints.Length == 0)
			throw new InvalidOperationException("観測点データが空です");

		// 出力ディレクトリを作成
		Directory.CreateDirectory(outputDir);

		var startTime = replayData[0].Time;
		var endTime = replayData[^1].Time;
		var totalFrames = replayData.Length;
		var stationCount = observationPoints.Length;

		// メタデータを作成
		var metadata = new DatasetMetadata
		{
			Version = 1,
			StartTime = startTime.ToString("O"),
			EndTime = endTime.ToString("O"),
			TotalFrames = totalFrames,
			StationCount = stationCount,
			Stations = observationPoints.Select(p => new StationInfo
			{
				Code = p.Code,
				Name = p.Name,
				Region = p.Region,
				Lat = p.Location.Latitude,
				Lon = p.Location.Longitude,
			}).ToList()
		};

		// 地震情報を変換
		var earthquakeInfos = ConvertEarthquakes(earthquakes, replayData);

		// 進捗報告
		progress?.Report(new ExportProgress
		{
			CurrentFrame = 0,
			TotalFrames = totalFrames,
			Message = "震度データを処理中..."
		});

		// 震度データをバイナリで書き出し（float32[totalFrames][stationCount]）
		var intensityPath = Path.Combine(outputDir, "intensity.bin");
		await using (var fs = new FileStream(intensityPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
		await using (var writer = new BinaryWriter(fs))
		{
			for (var frameIndex = 0; frameIndex < totalFrames; frameIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var data = replayData[frameIndex];

				// 強震モニタ画像から震度を取得
				float[] intensities;
				if (data.Images.TryGetValue(KyoshinMonitorImageReplayData.ImageType.Shindo, out var imageBytes))
				{
					using var bitmap = SKBitmap.Decode(imageBytes);
					intensities = bitmap != null
						? ExtractIntensitiesFromImage(bitmap, observationPoints)
						: CreateNanArray(stationCount);
				}
				else
				{
					intensities = CreateNanArray(stationCount);
				}

				// float32としてバイナリ書き出し
				foreach (var intensity in intensities)
					writer.Write(intensity);

				// 進捗報告（100フレームごと）
				if (frameIndex % 100 == 0)
				{
					progress?.Report(new ExportProgress
					{
						CurrentFrame = frameIndex,
						TotalFrames = totalFrames,
						Message = $"震度データを処理中... ({frameIndex}/{totalFrames})"
					});
				}
			}
		}

		// メタデータをJSON出力
		progress?.Report(new ExportProgress
		{
			CurrentFrame = totalFrames,
			TotalFrames = totalFrames,
			Message = "メタデータを出力中..."
		});

		var metadataPath = Path.Combine(outputDir, "metadata.json");
		var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);

		// 地震情報をJSON出力
		var earthquakesPath = Path.Combine(outputDir, "earthquakes.json");
		var earthquakesJson = JsonSerializer.Serialize(earthquakeInfos, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		await File.WriteAllTextAsync(earthquakesPath, earthquakesJson, cancellationToken);

		progress?.Report(new ExportProgress
		{
			CurrentFrame = totalFrames,
			TotalFrames = totalFrames,
			Message = "エクスポート完了"
		});
	}

	/// <summary>
	/// 画像から各観測点の震度を抽出する
	/// </summary>
	private static float[] ExtractIntensitiesFromImage(SKBitmap bitmap, RealtimeObservationPoint[] points)
	{
		var intensities = new float[points.Length];

		for (var i = 0; i < points.Length; i++)
		{
			var point = points[i];
			var color = bitmap.GetPixel(point.ImageLocation.X, point.ImageLocation.Y);

			if (color.Alpha != 255)
			{
				intensities[i] = float.NaN;
				continue;
			}

			var intensity = ColorConverter.ConvertToIntensityFromScale(
				ColorConverter.ConvertToScaleAtPolynomialInterpolation(color));
			intensities[i] = (float)intensity;
		}

		return intensities;
	}

	/// <summary>
	/// NaNで埋められた配列を作成する
	/// </summary>
	private static float[] CreateNanArray(int length)
	{
		var array = new float[length];
		Array.Fill(array, float.NaN);
		return array;
	}

	/// <summary>
	/// 地震情報をエクスポート用形式に変換する
	/// </summary>
	private static List<EarthquakeInfo> ConvertEarthquakes(
		ShindoDbEarthquake[] earthquakes,
		KyoshinMonitorImageReplayData[] replayData)
	{
		if (replayData.Length == 0)
			return [];

		var startTime = replayData[0].Time;
		var result = new List<EarthquakeInfo>();

		foreach (var eq in earthquakes)
		{
			if (eq.OccurrenceTime is not { } occTime)
				continue;

			// 発生時刻に最も近いフレームインデックスを探す
			var timeIndex = FindClosestFrameIndex(replayData, occTime);

			result.Add(new EarthquakeInfo
			{
				Id = eq.Id ?? "",
				TimeIndex = timeIndex,
				Time = occTime.ToString("O"),
				Name = eq.Name,
				Latitude = eq.Latitude,
				Longitude = eq.Longitude,
				Depth = eq.Depth,
				Magnitude = eq.Magnitude,
				MaxIntensity = eq.MaxI,
			});
		}

		return result.OrderBy(e => e.TimeIndex).ToList();
	}

	/// <summary>
	/// 指定時刻に最も近いフレームインデックスを探す
	/// </summary>
	private static int FindClosestFrameIndex(KyoshinMonitorImageReplayData[] replayData, DateTime targetTime)
	{
		var minDiff = double.MaxValue;
		var closestIndex = 0;

		for (var i = 0; i < replayData.Length; i++)
		{
			var diff = Math.Abs((replayData[i].Time - targetTime).TotalSeconds);
			if (diff < minDiff)
			{
				minDiff = diff;
				closestIndex = i;
			}
			// 既に離れ始めたら終了（時刻順にソートされている前提）
			else if (diff > minDiff)
			{
				break;
			}
		}

		return closestIndex;
	}
}
