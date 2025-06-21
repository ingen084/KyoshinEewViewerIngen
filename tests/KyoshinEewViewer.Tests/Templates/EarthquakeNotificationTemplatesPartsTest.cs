using KyoshinEewViewer.Series.Earthquake.Templates;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinMonitorLib;
using Scriban;
using System;

namespace KyoshinEewViewer.Tests.Templates;

public class EarthquakeNotificationTemplatesPartsTest
{
	/// <summary>
	/// 配列テンプレートテストケースデータ
	/// </summary>
	public class TestCaseData
	{
		public required string TestName { get; init; }
		public required string[] ExpectedResult { get; init; }
		public required EarthquakeInformationEvent EventData { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly TestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "VoiceNotificationParts_Normal",
				ExpectedResult = ["", "", "テスト県西部で", "最大震度4の地震が発生しました", "深さ20キロ", "マグニチュードは5.1です", ""],
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
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Training",
				ExpectedResult = ["これは訓練もしくは試験です", "", "テスト地方で", "最大震度2の地震が発生しました", "深さ10キロ", "マグニチュードは4.2です", ""],
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
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Cancelled",
				ExpectedResult = ["", "地震情報は取り消されました", "", "", "", "", ""],
				EventData = new EarthquakeInformationEvent(null)
				{
					LatestInformationName = "震源・震度に関する情報",
					EarthquakeId = "20241225123456",
					MaxIntensity = JmaIntensity.Int3,
					IsCancelled = true,
					IsTrainingOrTest = false
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_WithComment",
				ExpectedResult = ["", "", "ダミー県で", "最大震度4の地震が発生しました", "深さ30キロ", "マグニチュードは5.2です", "今後の地震に注意してください。"],
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
					Comment = "今後の地震に注意してください。"
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_VeryShallow",
				ExpectedResult = ["", "", "海底付近で", "最大震度5弱の地震が発生しました", "深さはごく浅い", "マグニチュードは6.3です", ""],
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
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_NoDepthData",
				ExpectedResult = ["", "", "架空県で", "最大震度3の地震が発生しました", "", "マグニチュードは4.5です", ""],
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
				}
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
	[MemberData(nameof(TestCaseData.TestCasesData), MemberType = typeof(TestCaseData))]
	public void VoiceNotificationParts_ShouldReturnExpectedResult(TestCaseData testCase)
	{
		// Arrange & Act
		var result = RenderTemplateArray(EarthquakeNotificationTemplates.VoiceNotificationParts, testCase.EventData);

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}