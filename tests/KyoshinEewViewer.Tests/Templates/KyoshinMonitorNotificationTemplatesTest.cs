using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Templates;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinMonitorLib;
using Scriban;

namespace KyoshinEewViewer.Tests.Templates;

public class KyoshinMonitorNotificationTemplatesTest
{
	/// <summary>
	/// テストケースデータ
	/// </summary>
	public class TestCaseData
	{
		public required string TestName { get; init; }
		public required string ExpectedResult { get; init; }
		public required object EventData { get; init; }
		public required string TemplateType { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly TestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "NewEew_Normal",
				ExpectedResult = "震度4/テスト県西部/M5.2/30km",
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
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "UpdateEew_WithIntensityOver",
				ExpectedResult = "震度5強以上/サンプル県東部/M6.1/10km",
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
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "FinalEew",
				ExpectedResult = "震度3/架空県南部/M4.8/45km",
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
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "CancelledEew",
				ExpectedResult = "震度2",
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
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "WarningEew",
				ExpectedResult = "震度5弱/テスト海域/M6.5/20km 【エリアA・エリアB】",
				EventData = new EewEvent(null, EewEventType.NewWarning)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 2,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int5Lower,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト海域",
					EpicenterLocation = new Location(35.5f, 140.5f),
					Magnitude = 6.5f,
					Depth = 20,
					IsTemporaryEpicenter = false,
					IsWarning = true,
					WarningAreaCodes = [100, 200],
					WarningAreaNames = ["エリアA", "エリアB"],
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "ReplayMode",
				ExpectedResult = "震度4/テスト県/M5.0/25km",
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
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "TemporaryEpicenter",
				ExpectedResult = "震度3/テスト県付近/M4.5",
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int3,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト県付近",
					EpicenterLocation = new Location(35.0f, 139.0f),
					Magnitude = 4.5f,
					Depth = 15,
					IsTemporaryEpicenter = true,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "NoEpicenterData",
				ExpectedResult = "震度2",
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int2,
					IsIntensityOver = false,
					EpicenterPlaceName = null,
					EpicenterLocation = null,
					Magnitude = null,
					Depth = null,
					IsTemporaryEpicenter = false,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewNotificationMessage"
			},
			new ()
			{
				TestName = "VoiceNotification_New",
				ExpectedResult = "緊急地震速報最大震度4テスト県西部",
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
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Update",
				ExpectedResult = "緊急地震速報最大震度5強以上サンプル県東部",
				EventData = new EewEvent(null, EewEventType.New)
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
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Cancel",
				ExpectedResult = "緊急地震速報は取り消されました",
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
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Warning",
				ExpectedResult = "緊急地震速報最大震度5弱テスト海域",
				EventData = new EewEvent(null, EewEventType.NewWarning)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 2,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int5Lower,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト海域",
					EpicenterLocation = new Location(35.5f, 140.5f),
					Magnitude = 6.5f,
					Depth = 20,
					IsTemporaryEpicenter = false,
					IsWarning = true,
					WarningAreaCodes = [100, 200],
					WarningAreaNames = ["エリアA", "エリアB"],
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Replay",
				ExpectedResult = "リプレイ中です緊急地震速報最大震度4テスト県",
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
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_TemporaryEpicenter",
				ExpectedResult = "緊急地震速報最大震度3テスト県付近",
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int3,
					IsIntensityOver = false,
					EpicenterPlaceName = "テスト県付近",
					EpicenterLocation = new Location(35.0f, 139.0f),
					Magnitude = 4.5f,
					Depth = 15,
					IsTemporaryEpicenter = true,
					IsWarning = false,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewVoiceNotification"
			},
			new ()
			{
				TestName = "ShakeDetected_Weak_Notification",
				ExpectedResult = "揺れ検知 弱い 関東地方",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Weak }, false),
				TemplateType = "ShakeDetectedNotificationMessage"
			},
			new ()
			{
				TestName = "ShakeDetected_Medium_Notification",
				ExpectedResult = "揺れ検知 普通 中部地方",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "中部", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Medium }, false),
				TemplateType = "ShakeDetectedNotificationMessage"
			},
			new ()
			{
				TestName = "ShakeDetected_Strong_Notification",
				ExpectedResult = "揺れ検知 強い 九州地方",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "九州", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, false),
				TemplateType = "ShakeDetectedNotificationMessage"
			},
			new ()
			{
				TestName = "ShakeDetected_Stronger_Notification",
				ExpectedResult = "[リプレイ] 揺れ検知 非常に強い 東北地方",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "東北", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Stronger }, true),
				TemplateType = "ShakeDetectedNotificationMessage"
			},
			new ()
			{
				TestName = "ShakeDetected_Weak_Voice",
				ExpectedResult = "弱い揺れを検知しました",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Weak }, false),
				TemplateType = "ShakeDetectedVoiceNotification"
			},
			new ()
			{
				TestName = "ShakeDetected_Medium_Voice",
				ExpectedResult = "揺れを検知しました",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Medium }, false),
				TemplateType = "ShakeDetectedVoiceNotification"
			},
			new ()
			{
				TestName = "ShakeDetected_Strong_Voice",
				ExpectedResult = "強い揺れを検知しました",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, false),
				TemplateType = "ShakeDetectedVoiceNotification"
			},
			new ()
			{
				TestName = "ShakeDetected_Stronger_Voice",
				ExpectedResult = "非常に強い揺れを検知しました",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Stronger }, false),
				TemplateType = "ShakeDetectedVoiceNotification"
			},
			new ()
			{
				TestName = "ShakeDetected_Replay_Voice",
				ExpectedResult = "リプレイ中です強い揺れを検知しました",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, true),
				TemplateType = "ShakeDetectedVoiceNotification"
			},
			// EEWタイトルテストケース
			new ()
			{
				TestName = "EewTitle_New",
				ExpectedResult = "緊急地震速報(01報)",
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 1,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int4,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewNotificationTitle"
			},
			new ()
			{
				TestName = "EewTitle_Final",
				ExpectedResult = "緊急地震速報(最終05報)",
				EventData = new EewEvent(null, EewEventType.Final)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 5,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int3,
					IsFinal = true,
					IsCancelled = false,
					IsReplay = false
				},
				TemplateType = "EewNotificationTitle"
			},
			new ()
			{
				TestName = "EewTitle_Cancelled",
				ExpectedResult = "[取消] 緊急地震速報(02報)",
				EventData = new EewEvent(null, EewEventType.Cancel)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 2,
					IsTrueCancelled = true,
					Intensity = JmaIntensity.Int2,
					IsCancelled = true,
					IsReplay = false
				},
				TemplateType = "EewNotificationTitle"
			},
			new ()
			{
				TestName = "EewTitle_Replay",
				ExpectedResult = "[リプレイ] 緊急地震速報(03報)",
				EventData = new EewEvent(null, EewEventType.New)
				{
					EewId = "20241225123456",
					EewSource = "気象庁",
					SerialNo = 3,
					IsTrueCancelled = false,
					Intensity = JmaIntensity.Int4,
					IsFinal = false,
					IsCancelled = false,
					IsReplay = true
				},
				TemplateType = "EewNotificationTitle"
			},
			// 揺れ検知タイトルテストケース
			new ()
			{
				TestName = "ShakeDetectedTitle_Normal",
				ExpectedResult = "揺れ検知",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, false),
				TemplateType = "ShakeDetectedNotificationTitle"
			},
			new ()
			{
				TestName = "ShakeDetectedTitle_Replay",
				ExpectedResult = "[リプレイ] 揺れ検知",
				EventData = new ShakeDetectedEvent(null, DateTime.Now, new KyoshinEvent(DateTime.Now.AddSeconds(-30), new RealtimeObservationPoint(new Core.Models.KyoshinMonitorObservationPoint.ObservationPointV2 { Code = "TEST", Name = "テスト", Region = "関東", Location = new(0, 0), Point = new(new(), new()), Type = ObservationPointType.K_NET })) { Level = KyoshinEventLevel.Strong }, true),
				TemplateType = "ShakeDetectedNotificationTitle"
			},
		];

		public static IEnumerable<object[]> TestCasesData =>
			TestCases.Select(testCase => new object[] { testCase });

		public override string ToString() => TestName;
	}

	/// <summary>
	/// Scribanテンプレートを適切なコンテキストでレンダリングするヘルパーメソッド
	/// </summary>
	private static string RenderTemplate(string templateText, object data)
		=> Template.Parse(templateText).Render(data, m => m.Name);

	[Theory]
	[MemberData(nameof(TestCaseData.TestCasesData), MemberType = typeof(TestCaseData))]
	public void TemplateRender_ShouldReturnExpectedResult(TestCaseData testCase)
	{
		// Arrange & Act
		string result;
		switch (testCase.TemplateType)
		{
			case "EewNotificationMessage":
				result = RenderTemplate(KyoshinMonitorNotificationTemplates.EewNotificationMessage, testCase.EventData);
				break;
			case "EewVoiceNotification":
				result = string.Join("", KyoshinMonitorNotificationTemplates.EewVoiceNotification.Select(template => Template.Parse(template).Render(testCase.EventData, m => m.Name)));
				break;
			case "ShakeDetectedNotificationMessage":
				result = RenderTemplate(KyoshinMonitorNotificationTemplates.ShakeDetectedNotificationMessage, testCase.EventData);
				break;
			case "ShakeDetectedVoiceNotification":
				result = string.Join("", KyoshinMonitorNotificationTemplates.ShakeDetectedVoiceNotification.Select(template => Template.Parse(template).Render(testCase.EventData, m => m.Name)));
				break;
			case "EewNotificationTitle":
				result = RenderTemplate(KyoshinMonitorNotificationTemplates.EewNotificationTitle, testCase.EventData);
				break;
			case "ShakeDetectedNotificationTitle":
				result = RenderTemplate(KyoshinMonitorNotificationTemplates.ShakeDetectedNotificationTitle, testCase.EventData);
				break;
			default:
				throw new ArgumentException($"Unknown template type: {testCase.TemplateType}");
		}

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}
