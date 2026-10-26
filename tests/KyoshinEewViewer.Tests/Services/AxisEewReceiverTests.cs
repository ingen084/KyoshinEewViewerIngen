using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message;
using AxisEewHypocenter = KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message.EewHypocenter;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace KyoshinEewViewer.Tests.Services;

public class AxisEewReceiverTests
{
	private const string SampleEewJson = """
		{
			"Title": "緊急地震速報（予報）",
			"OriginDateTime": "2022-06-17T00:51:25+09:00",
			"ReportDateTime": "2022-06-17T00:52:19+09:00",
			"EventID": "20220617005133",
			"Serial": 9,
			"Hypocenter": {
				"Code": 601,
				"Name": "徳島県南部",
				"Coordinate": [134.6, 33.9],
				"Depth": "50km",
				"Description": "北緯33.9度 東経134.6度 深さ約50km"
			},
			"Intensity": "4",
			"Magnitude": "5.0",
			"Flag": {
				"is_final": true,
				"is_cancel": false,
				"is_training": false
			},
			"Forecast": [
				{
					"Code": 610,
					"Name": "香川県東部",
					"Intensity": { "From": "4", "To": "4", "Description": "最大震度4程度" }
				},
				{
					"Code": 611,
					"Name": "香川県西部",
					"Intensity": { "From": "4", "To": "4", "Description": "最大震度4程度" }
				}
			],
			"Text": ""
		}
		""";

	[Fact(DisplayName = "AXISのEEWサンプルから震源_震度_予報地域を取り込む")]
	public void AxisのEEWサンプルから震源_震度_予報地域を取り込む()
	{
		var (eew, updatedTime) = ReceiveEew(JsonSerializer.Deserialize<EewMessage>(SampleEewJson));

		Assert.Equal("20220617005133", eew.Id);
		Assert.Equal(EewSource.Axis, eew.Source);
		Assert.Equal("AXIS", eew.DisplaySource);
		Assert.Equal(9, eew.SerialNo);
		Assert.True(eew.IsFinal);
		Assert.False(eew.IsWarning);
		Assert.Equal(JmaIntensity.Int4, eew.MaxIntensity);
		Assert.False(eew.IsIntensityOver);
		Assert.Equal(new DateTime(2022, 6, 17, 0, 51, 25), eew.Hypocenter?.OccurrenceTime);
		Assert.Equal("徳島県南部", eew.Hypocenter?.Place);
		Assert.Equal(50, eew.Hypocenter?.Depth);
		Assert.Equal(5.0f, eew.Hypocenter?.Magnitude);
		Assert.Equal(33.9f, eew.Hypocenter?.Location?.Latitude);
		Assert.Equal(134.6f, eew.Hypocenter?.Location?.Longitude);
		Assert.Equal(new DateTime(2022, 6, 17, 0, 52, 19), eew.ReceiveTime);
		Assert.Equal(new DateTime(2022, 6, 17, 0, 52, 19), updatedTime);

		Assert.NotNull(eew.IntensityForecastMap);
		Assert.Equal(JmaIntensity.Int4, eew.IntensityForecastMap[610]);
		Assert.Equal(JmaIntensity.Int4, eew.IntensityForecastMap[611]);
		Assert.Null(eew.WarningAreas);
	}

	[Fact(DisplayName = "AXISのキャンセル報は震源情報がなくてもキャンセルとして扱う")]
	public void Axisのキャンセル報は震源情報がなくてもキャンセルとして扱う()
	{
		var message = CreateMessage("緊急地震速報（警報）", "7");
		message.Hypocenter = null!;
		message.Flag!.IsCancel = true;
		var controller = new Mock<IEewController>();

		CreateReceiver(controller).Receive(message);

		controller.Verify(c => c.Cancelled("20261017083012", new DateTime(2026, 10, 17, 8, 30, 20)), Times.Once);
		controller.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "AXISの訓練報は取り込まない")]
	public void Axisの訓練報は取り込まない()
	{
		var message = CreateMessage("緊急地震速報（予報）", "4");
		message.Flag!.IsTraining = true;
		var controller = new Mock<IEewController>();

		CreateReceiver(controller).Receive(message);

		controller.VerifyNoOtherCalls();
	}

	[Fact(DisplayName = "AXISの必要な情報が欠けた電文はEewControllerへ渡さない")]
	public void Axisの必要な情報が欠けた電文はEewControllerへ渡さない()
	{
		(string Name, Func<EewMessage?> Create)[] cases =
		[
			("電文が null", () => null),
			("EventID が null", () => Modify(m => m.EventID = null)),
			("EventID が空", () => Modify(m => m.EventID = "")),
			("EventID が空白", () => Modify(m => m.EventID = " ")),
			("取消報の EventID が空", () => Modify(m => { m.EventID = ""; m.Flag!.IsCancel = true; })),
			("Flag がない", () => Modify(m => m.Flag = null)),
			("Hypocenter がない", () => Modify(m => m.Hypocenter = null!)),
			("Depth が null", () => Modify(m => m.Hypocenter.Depth = null)),
			("Depth が空", () => Modify(m => m.Hypocenter.Depth = "")),
			("Depth が数値でない", () => Modify(m => m.Hypocenter.Depth = "不明")),
			("Depth が単位のみ", () => Modify(m => m.Hypocenter.Depth = "km")),
		];

		foreach (var (name, create) in cases)
		{
			var controller = new Mock<IEewController>();

			CreateReceiver(controller).Receive(create());

			var exception = Record.Exception(controller.VerifyNoOtherCalls);
			Assert.True(exception == null, $"{name}: {exception?.Message}");
		}

		static EewMessage Modify(Action<EewMessage> modify)
		{
			var message = CreateMessage("緊急地震速報（予報）", "4");
			modify(message);
			return message;
		}
	}

	[Theory(DisplayName = "AXISの警報状態をタイトル_本文_予想最大震度から判定する")]
	[InlineData("緊急地震速報（警報）", "", "4", true)]
	[InlineData("緊急地震速報（予報）", "強い揺れに警戒してください", "4", true)]
	[InlineData("緊急地震速報（予報）", "", "5-", true)]
	[InlineData("緊急地震速報（予報）", "", "7", true)]
	[InlineData("緊急地震速報（予報）", "", "4", false)]
	[InlineData("緊急地震速報（予報）", "", "不明", false)]
	[InlineData("緊急地震速報（予報）", "", "-1", false)]
	public void Axisの警報状態をタイトル_本文_予想最大震度から判定する(string title, string text, string intensity, bool expected)
	{
		var message = CreateMessage(title, intensity);
		message.Text = text;

		var (eew, _) = ReceiveEew(message);

		Assert.Equal(expected, eew.IsWarning);
	}

	[Fact(DisplayName = "AXISの予報地域は地域ごとの最大震度を採用し_異常値と不明を除外する")]
	public void Axisの予報地域は地域ごとの最大震度を採用し_異常値と不明を除外する()
	{
		var message = CreateMessage("緊急地震速報（警報）", "6-",
			Forecast(610, "香川県東部", "4", "4"),
			Forecast(610, "香川県東部", "5-", "5-"),
			Forecast(611, "香川県西部", "5-", "5-"),
			Forecast(611, "香川県西部", "-1", "-1"),
			Forecast(612, "愛媛県東予", "異常", "異常"),
			Forecast(613, "徳島県北部", "6-", "over"),
			Forecast(614, "徳島県南部", "不明", "不明"));

		var (eew, _) = ReceiveEew(message);

		Assert.NotNull(eew.IntensityForecastMap);
		Assert.Equal(3, eew.IntensityForecastMap.Count);
		Assert.Equal(JmaIntensity.Int5Lower, eew.IntensityForecastMap[610]);
		Assert.Equal(JmaIntensity.Int5Lower, eew.IntensityForecastMap[611]);
		Assert.Equal(JmaIntensity.Int6Lower, eew.IntensityForecastMap[613]);
	}

	[Fact(DisplayName = "AXISの予想最大震度と同じ地域が以上の場合のみ最大震度を以上とする")]
	public void Axisの予想最大震度と同じ地域が以上の場合のみ最大震度を以上とする()
	{
		var over = CreateMessage("緊急地震速報（警報）", "6-",
			Forecast(610, "香川県東部", "5-", "5-"),
			Forecast(613, "徳島県北部", "6-", "over"));
		Assert.True(ReceiveEew(over).Eew.IsIntensityOver);

		var lowerAreaOver = CreateMessage("緊急地震速報（警報）", "5-",
			Forecast(610, "香川県東部", "5-", "5-"),
			Forecast(611, "香川県西部", "4", "over"));
		Assert.False(ReceiveEew(lowerAreaOver).Eew.IsIntensityOver);
	}

	private static AxisEewReceiver CreateReceiver(Mock<IEewController> controller)
		=> new(NullLogger<AxisEewReceiver>.Instance, controller.Object);

	/// <summary>
	/// 電文を受信させ、EewController.Update に1回だけ渡された EEW と受信時刻を返す
	/// </summary>
	private static (Eew Eew, DateTime UpdatedTime) ReceiveEew(EewMessage? message)
	{
		var controller = new Mock<IEewController>();
		var received = new List<(Eew, DateTime)>();
		controller
			.Setup(c => c.Update(It.IsAny<Eew>(), It.IsAny<DateTime>()))
			.Callback<Eew, DateTime>((eew, updatedTime) => received.Add((eew, updatedTime)));

		CreateReceiver(controller).Receive(message);

		controller.Verify(c => c.Update(It.IsAny<Eew>(), It.IsAny<DateTime>()), Times.Once);
		controller.VerifyNoOtherCalls();
		return received.Single();
	}

	private static EewMessage CreateMessage(string title, string intensity, params EewForecast[] forecast)
		=> new()
		{
			Title = title,
			OriginDateTime = new DateTimeOffset(2026, 10, 17, 8, 30, 12, TimeSpan.FromHours(9)),
			ReportDateTime = new DateTimeOffset(2026, 10, 17, 8, 30, 20, TimeSpan.FromHours(9)),
			EventID = "20261017083012",
			Serial = 3,
			Hypocenter = new AxisEewHypocenter
			{
				Code = 601,
				Name = "徳島県南部",
				Coordinate = [134.6f, 33.9f],
				Depth = "50km",
				Description = "北緯33.9度 東経134.6度 深さ約50km",
			},
			Intensity = intensity,
			Magnitude = "5.0",
			Flag = new EewFlag { IsFinal = false, IsCancel = false, IsTraining = false },
			Forecast = forecast,
			Text = "",
		};

	private static EewForecast Forecast(int code, string name, string from, string to)
		=> new()
		{
			Code = code,
			Name = name,
			Intensity = new EewIntensity
			{
				From = from,
				To = to,
				Description = $"最大震度{to}",
			},
		};
}
