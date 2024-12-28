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
			Codes = [1, 2, 3],
			Names = ["テスト1"],
		},
	};

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
			Codes = [1, 2, 3],
			Names = ["テスト1"],
		},
	};
}
