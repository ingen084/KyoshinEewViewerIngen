using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Templates;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinMonitorLib;
using Scriban;
using System;

namespace KyoshinEewViewer.Tests.Templates;

public class KyoshinMonitorNotificationTemplatesPartsTest
{
	/// <summary>
	/// EEW配列テンプレートテストケースデータ
	/// </summary>
	public class EewTestCaseData
	{
		public required string TestName { get; init; }
		public required string[] ExpectedResult { get; init; }
		public required EewEvent EventData { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly EewTestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "EewVoiceParts_New_Normal",
				ExpectedResult = ["", "緊急地震速報", "最大震度4", "テスト県西部"],
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int4,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト県西部",
					EpicenterLocation = new Location(35.0f, 139.0f),
					Magnitude = 5.2f,
					Depth = 30,
					IsTemporaryEpicenter = false,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				}
			},
			new ()
			{
				TestName = "EewVoiceParts_Replay",
				ExpectedResult = ["リプレイ中です", "緊急地震速報", "最大震度4", "テスト県"],
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int4,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト県",
					EpicenterLocation = new Location(35.0f, 139.0f),
					Magnitude = 5.0f,
					Depth = 25,
					IsTemporaryEpicenter = false,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = true
				}
			},
			new ()
			{
				TestName = "EewVoiceParts_Cancel",
				ExpectedResult = ["", "緊急地震速報は取り消されました", "", ""],
				EventData = new EewEvent(null, EewEventType.Cancel)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 2,
					IsTrueCancelled = true,
					Intensity = JmaIntensity.Int2,
					IsIntensityOver = false,
					IsCancelled = true,
					IsReplay = false
				}
			},
			new ()
			{
				TestName = "EewVoiceParts_Final",
				ExpectedResult = ["", "緊急地震速報最終報", "最大震度3", "架空県南部"],
				EventData = new EewEvent(null, EewEventType.Final)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 5,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int3,
					IsIntensityOver = false,
					EpicenterPlaceName = "架空県南部",
					EpicenterLocation = new Location(34.0f, 136.0f),
					Magnitude = 4.8f,
					Depth = 45,
					IsTemporaryEpicenter = false,
					IsWarning = false,
					IsFinal = true,
					IsCancelled = false,
					IsReplay = false
				}
			},
			new ()
			{
				TestName = "EewVoiceParts_IntensityOver",
				ExpectedResult = ["", "緊急地震速報", "最大震度5強以上", "サンプル県東部"],
				EventData = new EewEvent(null, EewEventType.UpdateNewSerial)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 3,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int5Upper,
					IsIntensityOver = true,
					EpicenterPlaceName = "サンプル県東部",
					EpicenterLocation = new Location(36.0f, 140.0f),
					Magnitude = 6.1f,
					Depth = 10,
					IsTemporaryEpicenter = false,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				}
			},
		];

		public static IEnumerable<object[]> TestCasesData =>
			TestCases.Select(testCase => new object[] { testCase });

		public override string ToString() => TestName;
	}

	/// <summary>
	/// 揺れ検知配列テンプレートテストケースデータ
	/// </summary>
	public class ShakeDetectedTestCaseData
	{
		public required string TestName { get; init; }
		public required string[] ExpectedResult { get; init; }
		public required ShakeDetectedEvent EventData { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly ShakeDetectedTestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "ShakeDetectedVoiceParts_Strong",
				ExpectedResult = ["", "強い揺れを検知しました"],
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new KyoshinMonitorLib.ObservationPoint { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), OldLocation = new(0, 0), Point = new(0, 0), ClassificationId = 1, PrefectureClassificationId = 1, Type = KyoshinMonitorLib.ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, false)
			},
			new ()
			{
				TestName = "ShakeDetectedVoiceParts_Replay",
				ExpectedResult = ["リプレイ中です", "強い揺れを検知しました"],
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new KyoshinMonitorLib.ObservationPoint { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), OldLocation = new(0, 0), Point = new(0, 0), ClassificationId = 1, PrefectureClassificationId = 1, Type = KyoshinMonitorLib.ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, true)
			},
			new ()
			{
				TestName = "ShakeDetectedVoiceParts_Weak",
				ExpectedResult = ["", "弱い揺れを検知しました"],
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new KyoshinMonitorLib.ObservationPoint { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), OldLocation = new(0, 0), Point = new(0, 0), ClassificationId = 1, PrefectureClassificationId = 1, Type = KyoshinMonitorLib.ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Weak }, false)
			},
			new ()
			{
				TestName = "ShakeDetectedVoiceParts_Stronger",
				ExpectedResult = ["", "非常に強い揺れを検知しました"],
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new KyoshinMonitorLib.ObservationPoint { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), OldLocation = new(0, 0), Point = new(0, 0), ClassificationId = 1, PrefectureClassificationId = 1, Type = KyoshinMonitorLib.ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Stronger }, false)
			},
		];

		public static IEnumerable<object[]> TestCasesData =>
			TestCases.Select(testCase => new object[] { testCase });

		public override string ToString() => TestName;
	}

	/// <summary>
	/// 配列テンプレートを適切なコンテキストでレンダリングするヘルパーメソッド
	/// </summary>
	private static string[] RenderTemplateArray(string[] templateArray, object data)
		=> templateArray.Select(template => Template.Parse(template).Render(data, m => m.Name)).ToArray();

	[Theory]
	[MemberData(nameof(EewTestCaseData.TestCasesData), MemberType = typeof(EewTestCaseData))]
	public void EewVoiceNotificationParts_ShouldReturnExpectedResult(EewTestCaseData testCase)
	{
		// Arrange & Act
		var result = RenderTemplateArray(KyoshinMonitorNotificationTemplates.EewVoiceNotificationParts, testCase.EventData);

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}

	[Theory]
	[MemberData(nameof(ShakeDetectedTestCaseData.TestCasesData), MemberType = typeof(ShakeDetectedTestCaseData))]
	public void ShakeDetectedVoiceNotificationParts_ShouldReturnExpectedResult(ShakeDetectedTestCaseData testCase)
	{
		// Arrange & Act
		var result = RenderTemplateArray(KyoshinMonitorNotificationTemplates.ShakeDetectedVoiceNotificationParts, testCase.EventData);

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}