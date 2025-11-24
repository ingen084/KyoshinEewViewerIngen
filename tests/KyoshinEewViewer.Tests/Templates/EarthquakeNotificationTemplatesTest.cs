using KyoshinEewViewer.Series.Earthquake.Templates;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinMonitorLib;
using Scriban;

namespace KyoshinEewViewer.Tests.Templates;

public class EarthquakeNotificationTemplatesTest
{
	/// <summary>
	/// テストケースデータ
	/// </summary>
	public class TestCaseData
	{
		public required string TestName { get; init; }
		public required string ExpectedResult { get; init; }
		public required EarthquakeInformationEvent EventData { get; init; }
		public required string TemplateType { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly TestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "NormalCase",
				ExpectedResult = "最大震度4/12:34/テスト県テスト地方/30km/M5.5",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int4,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "テスト県テスト地方",
						Location: null,
						Magnitude: 5.5f,
						MagnitudeAlternativeText: null,
						Depth: 30,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsHypocenterOnly = false,
					IsDetailIntensityApplied = true,
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "CancelledCase",
				ExpectedResult = "[取消] 最大震度3",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int3,
					IsCancelled = true,
					IsTrainingOrTest = false,
					IsHypocenterOnly = false,
					IsDetailIntensityApplied = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "TrainingCase",
				ExpectedResult = "[訓練/試験] 最大震度2",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int2,
					IsCancelled = false,
					IsTrainingOrTest = true,
					IsHypocenterOnly = false,
					IsDetailIntensityApplied = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "TestCase",
				ExpectedResult = "[テスト] 最大震度1",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int1,
					IsCancelled = false,
					IsTrainingOrTest = false,
					IsTest = true,
					IsHypocenterOnly = false,
					IsDetailIntensityApplied = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "VeryShallowDepth",
				ExpectedResult = "最大震度5弱/12:34/サンプル海域/ごく浅い/M6.2",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int5Lower,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "サンプル海域",
						Location: null,
						Magnitude: 6.2f,
						MagnitudeAlternativeText: null,
						Depth: 5,
						IsNoDepthData: false,
						IsVeryShallow: true,
						IsForeign: false
					),
					IsHypocenterOnly = true,
					IsDetailIntensityApplied = false,
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "AlternativeMagnitude",
				ExpectedResult = "最大震度6弱/12:34/ダミー県ダミー地方/15km/M7.0以上",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int6Lower,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "ダミー県ダミー地方",
						Location: null,
						Magnitude: 0f,
						MagnitudeAlternativeText: "M7.0以上",
						Depth: 15,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsHypocenterOnly = true,
					IsDetailIntensityApplied = false,
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "NoDepthData",
				ExpectedResult = "最大震度4/12:34/架空県架空地方/M5.0",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int4,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "架空県架空地方",
						Location: null,
						Magnitude: 5.0f,
						MagnitudeAlternativeText: null,
						Depth: 0,
						IsNoDepthData: true,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsHypocenterOnly = true,
					IsDetailIntensityApplied = false,
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "UnknownIntensity",
				ExpectedResult = "最大震度不明/12:34/テスト海中部/400km/M4.5",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Unknown,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "テスト海中部",
						Location: null,
						Magnitude: 4.5f,
						MagnitudeAlternativeText: null,
						Depth: 400,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsHypocenterOnly = true,
					IsDetailIntensityApplied = false,
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = null
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "VoiceNotification",
				ExpectedResult = "テスト県西部で最大震度4の地震が発生しました深さ20キロマグニチュードは5.1です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int4,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "テスト県西部",
						Location: null,
						Magnitude: 5.1f,
						MagnitudeAlternativeText: null,
						Depth: 20,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Cancelled",
				ExpectedResult = "地震情報は取り消されました",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int3,
					IsCancelled = true,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Training",
				ExpectedResult = "これは訓練もしくは試験ですテスト地方で最大震度2の地震が発生しました深さ10キロマグニチュードは4.2です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int2,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "テスト地方",
						Location: null,
						Magnitude: 4.2f,
						MagnitudeAlternativeText: null,
						Depth: 10,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = true
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Test",
				ExpectedResult = "ワークフローのテストですサンプル県で最大震度1の地震が発生しました深さ5キロマグニチュードは3.8です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int1,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "サンプル県",
						Location: null,
						Magnitude: 3.8f,
						MagnitudeAlternativeText: null,
						Depth: 5,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false,
					IsTest = true
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_NoDepthData",
				ExpectedResult = "架空県で最大震度3の地震が発生しましたマグニチュードは4.5です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int3,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "架空県",
						Location: null,
						Magnitude: 4.5f,
						MagnitudeAlternativeText: null,
						Depth: 0,
						IsNoDepthData: true,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_VeryShallow",
				ExpectedResult = "海底付近で最大震度5弱の地震が発生しました深さはごく浅いマグニチュードは6.3です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int5Lower,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "海底付近",
						Location: null,
						Magnitude: 6.3f,
						MagnitudeAlternativeText: null,
						Depth: 3,
						IsNoDepthData: false,
						IsVeryShallow: true,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_WithComment",
				ExpectedResult = "ダミー県で最大震度4の地震が発生しました深さ30キロマグニチュードは5.2です今後の地震に注意してください",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int4,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "ダミー県",
						Location: null,
						Magnitude: 5.2f,
						MagnitudeAlternativeText: null,
						Depth: 30,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false,
					Comment = "今後の地震に注意してください"
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_NoHypocenter",
				ExpectedResult = "最大震度2の地震が発生しました",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int2,
					Hypocenter = null,
					IsCancelled = false,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_AlternativeMagnitude",
				ExpectedResult = "遠方沖で最大震度5強の地震が発生しました深さ50キロマグニチュードは7.0です",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int5Upper,
					Hypocenter = new EarthquakeInformationEventHypocenter(
						OccurrenceAt: DateTime.Parse("2024-12-25 12:34:56"),
						PlaceName: "遠方沖",
						Location: null,
						Magnitude: 7.0f,
						MagnitudeAlternativeText: "M7.0以上",
						Depth: 50,
						IsNoDepthData: false,
						IsVeryShallow: false,
						IsForeign: false
					),
					IsCancelled = false,
					IsTrainingOrTest = false
				},
				TemplateType = "VoiceNotification"
			},
			// タイトルテストケース
			new ()
			{
				TestName = "NotificationTitle_Normal",
				ExpectedResult = "震源・震度に関する情報",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int4,
					IsCancelled = false,
					IsTrainingOrTest = false,
					IsTest = false
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Cancelled",
				ExpectedResult = "[取消] 震源・震度に関する情報",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int3,
					IsCancelled = true,
					IsTrainingOrTest = false,
					IsTest = false
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Training",
				ExpectedResult = "[訓練/試験] 震源・震度に関する情報",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int2,
					IsCancelled = false,
					IsTrainingOrTest = true,
					IsTest = false
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Test",
				ExpectedResult = "[テスト] 震源・震度に関する情報",
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int1,
					IsCancelled = false,
					IsTrainingOrTest = false,
					IsTest = true
				},
				TemplateType = "NotificationTitle"
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
		var templateText = testCase.TemplateType switch
		{
			"NotificationMessage" => EarthquakeNotificationTemplates.NotificationMessage,
			"VoiceNotification" => string.Join("", EarthquakeNotificationTemplates.VoiceNotificationParts.Select(template => Template.Parse(template).Render(testCase.EventData, m => m.Name))),
			"NotificationTitle" => EarthquakeNotificationTemplates.NotificationTitle,
			_ => throw new ArgumentException($"Unknown template type: {testCase.TemplateType}")
		};

		string result;
		if (testCase.TemplateType == "VoiceNotification")
		{
			result = templateText; // すでにレンダリング済み
		}
		else
		{
			result = RenderTemplate(templateText, testCase.EventData);
		}

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}