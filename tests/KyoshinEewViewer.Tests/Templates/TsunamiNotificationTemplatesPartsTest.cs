using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Series.Tsunami.Templates;
using KyoshinEewViewer.Series.Tsunami.Workflow;
using Scriban;
using System;

namespace KyoshinEewViewer.Tests.Templates;

public class TsunamiNotificationTemplatesPartsTest
{
	/// <summary>
	/// 津波情報配列テンプレートテストケースデータ
	/// </summary>
	public class TestCaseData
	{
		public required string TestName { get; init; }
		public required string[] ExpectedResult { get; init; }
		public required TsunamiInformationEvent EventData { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly TestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "VoiceNotificationParts_MajorWarning",
				ExpectedResult = ["", "大津波警報が発表されました", "大津波警報の対象地域は沿岸A、沿岸Bです"],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						MajorWarningAreas = [
							new TsunamiWarningArea(100, "沿岸A", "5m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:00:00") },
							new TsunamiWarningArea(200, "沿岸B", "3m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:15:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Training",
				ExpectedResult = ["訓練です", "津波警報が発表されました", "津波警報の対象地域はテスト沿岸です"],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = "訓練",
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						WarningAreas = [
							new TsunamiWarningArea(800, "テスト沿岸", "2m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:00:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Cancelled",
				ExpectedResult = ["", "津波警報等はすべて解除されました", ""],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56")
					},
					PreviousLevel = TsunamiLevel.Warning
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Warning",
				ExpectedResult = ["", "津波警報が発表されました", "津波警報の対象地域は沿岸C、沿岸Dです"],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						WarningAreas = [
							new TsunamiWarningArea(300, "沿岸C", "2m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:30:00") },
							new TsunamiWarningArea(400, "沿岸D", "1m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:45:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Advisory",
				ExpectedResult = ["", "津波注意報が発表されました", "津波注意報の対象地域は沿岸E、沿岸Fです"],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						AdvisoryAreas = [
							new TsunamiWarningArea(500, "沿岸E", "0.5m", "注意報") { ArrivalTime = DateTime.Parse("2024-12-25 14:00:00") },
							new TsunamiWarningArea(600, "沿岸F", "0.3m", "注意報") { ArrivalTime = DateTime.Parse("2024-12-25 14:30:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				}
			},
			new ()
			{
				TestName = "VoiceNotificationParts_Forecast",
				ExpectedResult = ["", "津波予報が発表されました", ""],
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						ForecastAreas = [
							new TsunamiWarningArea(700, "沿岸G", "若干の海面変動", "予報") { ArrivalTime = DateTime.Parse("2024-12-25 15:00:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
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
		var result = RenderTemplateArray(TsunamiNotificationTemplates.VoiceNotificationParts, testCase.EventData);

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}