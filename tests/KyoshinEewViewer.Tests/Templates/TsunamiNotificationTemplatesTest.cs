using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Series.Tsunami.Templates;
using KyoshinEewViewer.Series.Tsunami.Workflow;
using KyoshinMonitorLib;
using Scriban;
using Scriban.Runtime;
using System;

namespace KyoshinEewViewer.Tests.Templates;

public class TsunamiNotificationTemplatesTest
{
	/// <summary>
	/// テストケースデータ
	/// </summary>
	public class TestCaseData
	{
		public required string TestName { get; init; }
		public required string ExpectedResult { get; init; }
		public required TsunamiInformationEvent EventData { get; init; }
		public required string TemplateType { get; init; }

		/// <summary>
		/// テストデータセット
		/// </summary>
		public static readonly TestCaseData[] TestCases =
		[
			new ()
			{
				TestName = "MajorWarning_New",
				ExpectedResult = "大津波警報が発表されました。\n\n 【沿岸A・沿岸B】",
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
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "Warning_Update",
				ExpectedResult = "津波注意報 は 津波警報 に切り替えられました。\n\n 【沿岸C・沿岸D】",
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
					PreviousLevel = TsunamiLevel.Advisory
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "Advisory_New",
				ExpectedResult = "津波注意報が発表されました。\n\n (沿岸E・沿岸F)",
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
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "Cancelled",
				ExpectedResult = "津波注意報は解除されました。\n\n",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56")
					},
					PreviousLevel = TsunamiLevel.Advisory
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "Forecast",
				ExpectedResult = "津波予報が発表されました。\n\n",
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
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "Training",
				ExpectedResult = "津波警報が発表されました。\n\n 【テスト沿岸】",
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
				},
				TemplateType = "NotificationMessage"
			},
			new ()
			{
				TestName = "VoiceNotification_MajorWarning",
				ExpectedResult = "大津波警報が発表されました大津波警報の対象地域は沿岸A、沿岸Bです",
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
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Warning",
				ExpectedResult = "津波警報が発表されました津波警報の対象地域は沿岸C、沿岸Dです",
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
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Advisory",
				ExpectedResult = "津波注意報が発表されました津波注意報の対象地域は沿岸E、沿岸Fです",
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
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Cancelled",
				ExpectedResult = "津波警報等はすべて解除されました",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56")
					},
					PreviousLevel = TsunamiLevel.Warning
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Forecast",
				ExpectedResult = "津波予報が発表されました",
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
				},
				TemplateType = "VoiceNotification"
			},
			new ()
			{
				TestName = "VoiceNotification_Training",
				ExpectedResult = "訓練です津波警報が発表されました津波警報の対象地域はテスト沿岸です",
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
				},
				TemplateType = "VoiceNotification"
			},
			// タイトルテストケース
			new ()
			{
				TestName = "NotificationTitle_MajorWarning",
				ExpectedResult = "大津波警報",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						MajorWarningAreas = [
							new TsunamiWarningArea(100, "沿岸A", "5m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:00:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Warning",
				ExpectedResult = "津波警報",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						WarningAreas = [
							new TsunamiWarningArea(300, "沿岸C", "2m", "警報") { ArrivalTime = DateTime.Parse("2024-12-25 13:30:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Advisory",
				ExpectedResult = "津波注意報",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56"),
						AdvisoryAreas = [
							new TsunamiWarningArea(500, "沿岸E", "0.5m", "注意報") { ArrivalTime = DateTime.Parse("2024-12-25 14:00:00") }
						]
					},
					PreviousLevel = TsunamiLevel.None
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Cancelled",
				ExpectedResult = "津波解除",
				EventData = new TsunamiInformationEvent(null)
				{
					TsunamiInfo = new TsunamiInfo
					{
						EventId = "20241225123456",
						SpecialState = null,
						ReportedAt = DateTime.Parse("2024-12-25 12:34:56")
					},
					PreviousLevel = TsunamiLevel.Advisory
				},
				TemplateType = "NotificationTitle"
			},
			new ()
			{
				TestName = "NotificationTitle_Training",
				ExpectedResult = "[訓練] 津波警報",
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
		string result;
		switch (testCase.TemplateType)
		{
			case "NotificationMessage":
				result = RenderTemplate(TsunamiNotificationTemplates.NotificationMessage, testCase.EventData);
				break;
			case "VoiceNotification":
				result = string.Join("", TsunamiNotificationTemplates.VoiceNotificationParts.Select(template => Template.Parse(template).Render(testCase.EventData, m => m.Name)));
				break;
			case "NotificationTitle":
				result = RenderTemplate(TsunamiNotificationTemplates.NotificationTitle, testCase.EventData);
				break;
			default:
				throw new ArgumentException($"Unknown template type: {testCase.TemplateType}");
		}

		// Assert
		Assert.Equal(testCase.ExpectedResult, result);
	}
}