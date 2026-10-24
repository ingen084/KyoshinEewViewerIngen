using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message;
using AxisEewHypocenter = KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message.EewHypocenter;
using KyoshinMonitorLib;
using System.Text.Json;

namespace KyoshinEewViewer.Tests.Services;

public class AxisEewMapperTests
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
		var message = JsonSerializer.Deserialize<EewMessage>(SampleEewJson);
		var result = AxisEewMapper.Map(message);

		Assert.Equal(AxisEewMessageKind.Eew, result.Kind);
		var eew = Assert.IsType<Eew>(result.Eew);
		Assert.Equal("20220617005133", eew.Id);
		Assert.Equal(EewSource.Axis, eew.Source);
		Assert.Equal("AXIS", eew.DisplaySource);
		Assert.Equal(9, eew.SerialNo);
		Assert.True(eew.IsFinal);
		Assert.False(eew.IsWarning);
		Assert.Equal(JmaIntensity.Int4, eew.MaxIntensity);
		Assert.Equal(new DateTime(2022, 6, 17, 0, 51, 25), eew.Hypocenter?.OccurrenceTime);
		Assert.Equal("徳島県南部", eew.Hypocenter?.Place);
		Assert.Equal(50, eew.Hypocenter?.Depth);
		Assert.Equal(5.0f, eew.Hypocenter?.Magnitude);
		Assert.Equal(33.9f, eew.Hypocenter?.Location?.Latitude);
		Assert.Equal(134.6f, eew.Hypocenter?.Location?.Longitude);
		Assert.Equal(new DateTime(2022, 6, 17, 0, 52, 19), result.ReportTime);

		Assert.NotNull(eew.IntensityForecastMap);
		Assert.Equal(JmaIntensity.Int4, eew.IntensityForecastMap[610]);
		Assert.Equal(JmaIntensity.Int4, eew.IntensityForecastMap[611]);
		Assert.Null(eew.WarningAreas);
	}

	[Fact(DisplayName = "AXISのキャンセル報は震源情報がなくてもキャンセルとして扱う")]
	public void Axisのキャンセル報は震源情報がなくてもキャンセルとして扱う()
	{
		var message = CreateMessage("緊急地震速報（警報）", "7");
		message.EventID = "20261017083012";
		message.Hypocenter = null!;
		message.Flag!.IsCancel = true;

		var result = AxisEewMapper.Map(message);

		Assert.Equal(AxisEewMessageKind.Cancel, result.Kind);
		Assert.Null(result.Eew);
		Assert.Equal("20261017083012", result.EventId);
		Assert.Equal(new DateTime(2026, 10, 17, 8, 30, 20), result.ReportTime);
	}

	[Fact(DisplayName = "AXISの訓練報は取り込まない")]
	public void Axisの訓練報は取り込まない()
	{
		var message = CreateMessage("緊急地震速報（予報）", "4");
		message.Flag!.IsTraining = true;

		Assert.Equal(AxisEewMessageKind.Ignore, AxisEewMapper.Map(message).Kind);
		Assert.Equal(AxisEewMessageKind.Ignore, AxisEewMapper.Map(null).Kind);
	}

	[Fact(DisplayName = "AXISの同じ地域コードが複数ある場合は最大震度を採用する")]
	public void Axisの同じ地域コードが複数ある場合は最大震度を採用する()
	{
		var message = CreateMessage("緊急地震速報（警報）", "5-",
			Forecast(610, "香川県東部", "4", "4"),
			Forecast(610, "香川県東部", "5-", "5-"));

		var eew = Assert.IsType<Eew>(AxisEewMapper.Map(message).Eew);

		Assert.Equal(JmaIntensity.Int5Lower, eew.IntensityForecastMap?[610]);
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
