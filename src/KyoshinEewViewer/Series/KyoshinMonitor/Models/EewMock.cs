using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

public static class EewMock 
{
	public static readonly Eew NORMAL = new()
	{
		Id = "a",
		Source = EewSource.KyoshinMonitor,
		IsFinal = false,
		SerialNo = 999,
		DisplaySource = "モックアップ",
		IsCancelled = false,
		IsTrueCancelled = false,
		ReceiveTime = DateTime.Now,
		MaxIntensity = JmaIntensity.Int3,
		IsIntensityOver = false,

		Hypocenter = new()
		{
			OccurrenceTime = DateTime.Now,
			Place = "通常テストのこれは長い震央地名",
			Location = new Location(0, 0),
			Magnitude = 9.9f,
			Depth = 999,
			IsTemporary = false,
		},

		IsWarning = false,
	};

	public static readonly Eew WARNING = new()
	{
		Id = "b",
		Source = EewSource.KyoshinMonitor,
		IsFinal = false,
		SerialNo = 999,
		DisplaySource = "モックアップ",
		IsCancelled = false,
		IsTrueCancelled = false,
		ReceiveTime = DateTime.Now,
		MaxIntensity = JmaIntensity.Int3,
		IsIntensityOver = false,
		Hypocenter = new ()
		{
			OccurrenceTime = DateTime.Now,
			Place = "警報テスト",
			Location = new Location(0, 0),
			Magnitude = 9.9f,
			Depth = 999,
			IsTemporary = false,
			Accuracy = new ()
			{
				IsLocked = false,
				DepthAccuracy = 1,
				LocationAccuracy = 1,
				MagnitudeAccuracy = 2,
			},
		},

		IsWarning = true,
		WarningAreas = new()
		{
			DisplaySource = "SignalNowProfessional",
			SerialNo = 1,
			Codes = [1, 2, 3],
			Names = ["テスト1"],
		},
	};

	/// <summary>
	/// 地点予測付きのモック 折りたたみ行と展開行を同時に確認できる
	/// </summary>
	public static readonly Eew POINT_FORECAST = WARNING with
	{
		Id = "f",
		MaxIntensity = JmaIntensity.Int5Lower,
		PointForecasts = [
			CreateMockPointForecast("自宅", 4.8, 12.4),
			CreateMockPointForecast("実家", 3.9, 38.2),
			CreateMockPointForecast("職場", 0.2, null),
		],
	};

	private static EewPointForecast CreateMockPointForecast(string name, double intensity, double? remainingSeconds)
	{
		var forecast = new EewPointForecast
		{
			EventId = "f",
			SourceKey = "mock",
			DisplaySource = "モックアップ",
			SerialNo = 5,
			PointName = name,
			Location = new Location(35, 139),
			RealtimeIntensity = intensity,
			ArrivalTime = remainingSeconds == null ? null : DateTime.Now.AddSeconds(remainingSeconds.Value),
			// 進捗バーが途中まで進んだ状態を確認できるよう、発生から少し経過した状態にする
			OccurrenceTime = DateTime.Now.AddSeconds(-10),
			ReceiveTime = DateTime.Now,
		};
		forecast.UpdateDisplayValues(DateTime.Now, intensity >= 4.5);
		return forecast;
	}

	public static readonly Eew CANCELLED = new()
	{
		Id = "c",
		Source = EewSource.KyoshinMonitor,
		IsFinal = false,
		SerialNo = 999,
		DisplaySource = "モックアップ",
		IsCancelled = true,
		IsTrueCancelled = false,
		ReceiveTime = DateTime.Now,
		MaxIntensity = JmaIntensity.Int3,
		IsIntensityOver = false,
		Hypocenter = new()
		{
			OccurrenceTime = DateTime.Now,
			Place = "キャンセルテスト",
			Location = new Location(0, 0),
			Magnitude = 9.9f,
			Depth = 999,
			IsTemporary = false,
			Accuracy = new()
			{
				IsLocked = false,
				DepthAccuracy = 1,
				LocationAccuracy = 1,
				MagnitudeAccuracy = 2,
			},
		},
		IsWarning = false,
	};

	public static readonly Eew TRUE_CANCELLED = new ()
	{
		Id = "d",
		Source = EewSource.KyoshinMonitor,
		IsFinal = false,
		SerialNo = 999,
		DisplaySource = "モックアップ",
		IsCancelled = true,
		IsTrueCancelled = true,
		ReceiveTime = DateTime.Now,
		MaxIntensity = JmaIntensity.Int3,
		IsIntensityOver = false,
		Hypocenter = new ()
		{
			OccurrenceTime = DateTime.Now,
			Place = "確定キャンセルテスト",
			Location = new Location(0, 0),
			Magnitude = 9.9f,
			Depth = 999,
			IsTemporary = false,
			Accuracy = new ()
			{
				IsLocked = false,
				DepthAccuracy = 1,
				LocationAccuracy = 1,
				MagnitudeAccuracy = 2,
			},
		},
		IsWarning = false,
	};

	public static readonly Eew WARNING_CANCELLED = new()
	{
		Id = "e",
		Source = EewSource.KyoshinMonitor,
		IsFinal = false,
		SerialNo = 999,
		DisplaySource = "モックアップ",
		IsCancelled = true,
		IsTrueCancelled = false,
		ReceiveTime = DateTime.Now,
		MaxIntensity = JmaIntensity.Int3,
		IsIntensityOver = false,
		Hypocenter = new()
		{
			OccurrenceTime = DateTime.Now,
			Place = "警報キャンセルテスト",
			Location = new Location(0, 0),
			Magnitude = 9.9f,
			Depth = 999,
			IsTemporary = false,
			Accuracy = new()
			{
				IsLocked = false,
				DepthAccuracy = 1,
				LocationAccuracy = 1,
				MagnitudeAccuracy = 2,
			},
		},
		IsWarning = true,
		WarningAreas = new()
		{
			DisplaySource = "SignalNowProfessional",
			SerialNo = 1,
			Codes = [1, 2, 3],
			Names = ["テスト1"],
		},
	};
}
