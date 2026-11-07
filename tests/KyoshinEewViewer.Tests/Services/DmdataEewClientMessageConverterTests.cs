using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinMonitorLib;
using System.Text.Json;

namespace KyoshinEewViewer.Tests.Services;

public class DmdataEewClientMessageConverterTests
{
	private static readonly DateTime ReceiveTime = new(2026, 9, 11, 17, 52, 41);

	private static DmdataEewClientMessage Parse(string json)
		=> JsonSerializer.Deserialize(json, DmdataEewClientSerializerContext.Default.DmdataEewClientMessage)
			?? throw new InvalidOperationException("メッセージを解釈できません");

	[Fact(DisplayName = "start と server-status のメッセージを解釈できる")]
	public void StartとServerStatusのメッセージを解釈できる()
	{
		var start = Parse("""{"type":"start","version":"1.3.1"}""");
		Assert.Equal("start", start.Type);
		Assert.Equal("1.3.1", start.Version);

		var status = Parse("""{"type":"server-status","status":"no-auth"}""");
		Assert.Equal("server-status", status.Type);
		Assert.Equal("no-auth", status.Status);
	}

	[Fact(DisplayName = "user-point のメッセージから予想地点の座標を取り出せ、未設定・共有しない・不正な値は座標なしになる")]
	public void UserPointのメッセージから予想地点の座標を取り出せる()
	{
		var message = Parse("""{"type":"user-point","location":[135.1111,35.1111]}""");
		Assert.Equal("user-point", message.Type);
		// [経度, 緯度] の順で送られてくる
		var location = Assert.IsType<Location>(DmdataEewClientMessageConverter.ToUserPointLocation(message));
		Assert.Equal(35.1111f, location.Latitude, 0.0001f);
		Assert.Equal(135.1111f, location.Longitude, 0.0001f);

		foreach (var json in new[]
		{
			"""{"type":"user-point","location":null}""",
			"""{"type":"user-point","location":false}""",
			"""{"type":"user-point"}""",
			"""{"type":"user-point","location":[135.1]}""",
			"""{"type":"user-point","location":["a","b"]}""",
			"""{"type":"user-point","location":[135.1,95.0]}""",
		})
			Assert.Null(DmdataEewClientMessageConverter.ToUserPointLocation(Parse(json)));
	}

	[Fact(DisplayName = "震源が確定した eew メッセージから EEW と地点予測へ変換される")]
	public void 震源が確定したEewメッセージからEEWと地点予測へ変換される()
	{
		var message = Parse("""
			{"type":"eew","eventId":"20240101160608","serial":"4","author":"気象庁本庁",
			 "pressTime":"2024-01-01T16:06:14+09:00","processTime":"2026-09-11T17:52:40.544+09:00",
			 "isCanceled":false,"isLastInfo":false,"isPlumOnly":false,"isLevelOver":false,"isWarning":true,
			 "epicenterName":"石川県能登地方","epicenterLocation":[137.2,37.5],"depth":10,"magnitude":5.7,
			 "originTime":"2026-09-11T17:52:32+09:00","arrivalTime":"2026-09-11T17:52:34+09:00",
			 "accuracy":{"epicenters":["3","9"],"depth":"3","magnitudeCalculation":"1","numberOfMagnitudeCalculation":"4"},
			 "maxInt":{"from":"5+","to":"over"},"maxLgInt":{"from":"1","to":"1"},
			 "regions":[{"code":"390","maxInt":"5-"},{"code":"200","maxInt":"1"},{"code":"998","maxInt":"不正な値"},{"code":"999","maxInt":"不明"}],
			 "pointForecast":{"sWave":{"status":"ok","time":"2026-09-11T17:55:55.634+09:00"},"lpgm":{"maxSva":"0.04","lgInt":"0"},"intensity":{"type":"attenuation","k":"-2.04","int":"0"}}}
			""");

		var eew = DmdataEewClientMessageConverter.ToEew(message, ReceiveTime);
		Assert.Equal("20240101160608", eew.Id);
		Assert.Equal(EewSource.DmdataEewClient, eew.Source);
		Assert.Equal("EEW Client(気象庁本庁)", eew.DisplaySource);
		Assert.Equal(4, eew.SerialNo);
		Assert.False(eew.IsCancelled);
		Assert.False(eew.IsFinal);
		Assert.True(eew.IsWarning);
		Assert.Equal(ReceiveTime, eew.ReceiveTime);
		Assert.Equal(JmaIntensity.Int5Upper, eew.MaxIntensity);
		Assert.True(eew.IsIntensityOver);

		var hypocenter = Assert.IsType<EewHypocenter>(eew.Hypocenter);
		Assert.Equal("石川県能登地方", hypocenter.Place);
		Assert.Equal(new DateTime(2026, 9, 11, 17, 52, 32), hypocenter.OccurrenceTime);
		// [経度, 緯度] の順で送られてくる
		Assert.NotNull(hypocenter.Location);
		Assert.Equal(37.5f, hypocenter.Location.Latitude, 0.001f);
		Assert.Equal(137.2f, hypocenter.Location.Longitude, 0.001f);
		Assert.Equal(10, hypocenter.Depth);
		Assert.Equal(5.7f, hypocenter.Magnitude);
		Assert.False(hypocenter.IsTemporary);
		var accuracy = Assert.IsType<EewHypocenterAccuracy>(hypocenter.Accuracy);
		Assert.Equal(3, accuracy.LocationAccuracy);
		Assert.Equal(3, accuracy.DepthAccuracy);
		Assert.Equal(1, accuracy.MagnitudeAccuracy);
		Assert.True(accuracy.IsLocked);

		// 解釈できない震度の区域は除外される
		Assert.NotNull(eew.CalculatedIntensityForecastMap);
		Assert.Equal(2, eew.CalculatedIntensityForecastMap.Count);
		Assert.Equal(JmaIntensity.Int5Lower, eew.CalculatedIntensityForecastMap[390]);
		Assert.Equal(JmaIntensity.Int1, eew.CalculatedIntensityForecastMap[200]);
		// 警報地域の情報は送られてこない
		Assert.Null(eew.WarningAreas);

		var userPointLocation = new Location(35.1111f, 135.1111f);
		var forecast = Assert.IsType<EewPointForecast>(DmdataEewClientMessageConverter.ToPointForecast(message, ReceiveTime, userPointLocation, DmdataEewClientMessageConverter.DefaultPointName));
		Assert.Equal("20240101160608", forecast.EventId);
		Assert.Equal(DmdataEewClientMessageConverter.PointForecastSourceKey, forecast.SourceKey);
		Assert.Equal("EEW Client", forecast.DisplaySource);
		Assert.Equal(DmdataEewClientMessageConverter.DefaultPointName, forecast.PointName);
		Assert.Equal(4, forecast.SerialNo);
		// user-point で共有された座標が使われる
		Assert.Equal(userPointLocation, forecast.Location);
		Assert.Equal(-2.04, forecast.RealtimeIntensity, 0.0001);
		Assert.Equal(JmaIntensity.Int0, forecast.Intensity);
		Assert.Equal(new DateTime(2026, 9, 11, 17, 55, 55, 634), forecast.ArrivalTime);
		Assert.Equal(new DateTime(2026, 9, 11, 17, 52, 32), forecast.OccurrenceTime);
		Assert.Equal(ReceiveTime, forecast.ReceiveTime);
	}

	[Fact(DisplayName = "PLUM法のみで震源が不明な場合は仮定震源要素として変換され、予測震度が無い地点予測は作られない")]
	public void PLUM法のみで震源が不明な場合は仮定震源要素として変換され予測震度が無い地点予測は作られない()
	{
		var message = Parse("""
			{"type":"eew","eventId":"20240101160608","serial":"1","author":"気象庁本庁",
			 "isCanceled":false,"isLastInfo":false,"isPlumOnly":true,"isLevelOver":false,"isWarning":false,
			 "epicenterName":"富山湾","epicenterLocation":[137.4,37.4],"depth":10,"magnitude":null,
			 "originTime":"2026-09-11T17:52:34+09:00","arrivalTime":"2026-09-11T17:52:34+09:00",
			 "accuracy":{"epicenters":["1","1"],"depth":"1","magnitudeCalculation":"1"},
			 "regions":[],
			 "pointForecast":{"sWave":{"status":"not-calculated","time":null},"lpgm":{"maxSva":null,"lgInt":null},"intensity":{"type":"attenuation","k":null,"int":null}}}
			""");

		var eew = DmdataEewClientMessageConverter.ToEew(message, ReceiveTime);
		Assert.Equal(JmaIntensity.Unknown, eew.MaxIntensity);
		Assert.False(eew.IsIntensityOver);
		var hypocenter = Assert.IsType<EewHypocenter>(eew.Hypocenter);
		Assert.Null(hypocenter.Magnitude);
		Assert.True(hypocenter.IsTemporary);
		Assert.False(hypocenter.Accuracy?.IsLocked);
		Assert.NotNull(eew.CalculatedIntensityForecastMap);
		Assert.Empty(eew.CalculatedIntensityForecastMap);

		Assert.Null(DmdataEewClientMessageConverter.ToPointForecast(message, ReceiveTime, null, DmdataEewClientMessageConverter.DefaultPointName));
	}

	[Fact(DisplayName = "計測震度が無い場合は震度階級の下限値で代用し、到達時刻が未計算なら到達時刻なしになる")]
	public void 計測震度が無い場合は震度階級の下限値で代用し到達時刻が未計算なら到達時刻なしになる()
	{
		var cases = new (string Int, double Expected)[]
		{
			("0", 0), ("1", 0.5), ("3", 2.5), ("5-", 4.5), ("5+", 5.0), ("6-", 5.5), ("7", 6.5),
		};
		foreach (var (intensity, expected) in cases)
		{
			var message = Parse($$$$"""
				{"type":"eew","eventId":"20240101160608","serial":"2","isCanceled":false,
				 "arrivalTime":"2026-09-11T17:52:34+09:00",
				 "pointForecast":{"sWave":{"status":"not-calculated","time":"2026-09-11T17:55:55+09:00"},"intensity":{"type":"plum","k":null,"int":"{{{{intensity}}}}"}}}
				""");
			var forecast = Assert.IsType<EewPointForecast>(DmdataEewClientMessageConverter.ToPointForecast(message, ReceiveTime, null, DmdataEewClientMessageConverter.DefaultPointName));
			Assert.Equal(expected, forecast.RealtimeIntensity);
			Assert.Equal("EEW Client (PLUM)", forecast.DisplaySource);
			// status が ok でない場合は time があっても採用しない
			Assert.Null(forecast.ArrivalTime);
			Assert.Null(forecast.OccurrenceTime);
		}
	}

	[Fact(DisplayName = "取消報は震源を持たない取消済み EEW に変換され、地点予測は作られない")]
	public void 取消報は震源を持たない取消済みEEWに変換され地点予測は作られない()
	{
		var message = Parse("""
			{"type":"eew","eventId":"20240101160608","serial":"5","author":"気象庁本庁",
			 "isCanceled":true,"isLastInfo":true,"isPlumOnly":false,"isLevelOver":false,"isWarning":false,
			 "pointForecast":{"sWave":{"status":"ok","time":"2026-09-11T17:55:55+09:00"},"intensity":{"type":"attenuation","k":"3.5","int":"4"}}}
			""");

		var eew = DmdataEewClientMessageConverter.ToEew(message, ReceiveTime);
		Assert.True(eew.IsCancelled);
		Assert.True(eew.IsTrueCancelled);
		Assert.True(eew.IsFinal);
		Assert.Equal(5, eew.SerialNo);
		Assert.Null(eew.Hypocenter);

		Assert.Null(DmdataEewClientMessageConverter.ToPointForecast(message, ReceiveTime, null, DmdataEewClientMessageConverter.DefaultPointName));
	}

	[Fact(DisplayName = "使用しない項目に想定外の値が入っていても緊急地震速報を受信でき、解釈できない最大震度は不明になる")]
	public void 使用しない項目に想定外の値が入っていても緊急地震速報を受信できる()
	{
		var message = Parse("""
			{"type":"eew","eventId":"20240101160608","serial":"2","isTest":true,"isCanceled":false,
			 "pressTime":"不明","processTime":"","arrivalTime":"2026-09-11T17:52:34+09:00",
			 "accuracy":{"epicenters":["1","1"],"depth":"1","magnitudeCalculation":"1","numberOfMagnitudeCalculation":"不明"},
			 "maxInt":{"from":"不正な値","to":"不正な値"},"maxLgInt":"不明",
			 "pointForecast":{"lpgm":{"maxSva":"","lgInt":5},"intensity":{"type":"attenuation","k":"3.5","int":"4"}}}
			""");

		Assert.True(message.IsTest);
		var eew = DmdataEewClientMessageConverter.ToEew(message, ReceiveTime);
		Assert.Equal(JmaIntensity.Unknown, eew.MaxIntensity);
		Assert.Equal(3.5, DmdataEewClientMessageConverter.ToPointForecast(message, ReceiveTime, null, DmdataEewClientMessageConverter.DefaultPointName)?.RealtimeIntensity);
	}

	[Fact(DisplayName = "ドキュメント上の名称 (regionForecasts / to) でも細分区域の予測震度を読み取れる")]
	public void ドキュメント上の名称でも細分区域の予測震度を読み取れる()
	{
		var message = Parse("""
			{"type":"eew","eventId":"20240101160608","serial":"3","isCanceled":false,
			 "arrivalTime":"2026-09-11T17:52:34+09:00",
			 "regionForecasts":[{"code":"390","to":"4"},{"code":"391","to":"3"}]}
			""");

		var eew = DmdataEewClientMessageConverter.ToEew(message, ReceiveTime);
		Assert.NotNull(eew.CalculatedIntensityForecastMap);
		Assert.Equal(JmaIntensity.Int4, eew.CalculatedIntensityForecastMap[390]);
		Assert.Equal(JmaIntensity.Int3, eew.CalculatedIntensityForecastMap[391]);
		// 発表官署が無い場合は提供元名のみになる
		Assert.Equal("EEW Client", eew.DisplaySource);
		// 発生時刻が無い場合は検知時刻で代用する
		Assert.Equal(new DateTime(2026, 9, 11, 17, 52, 34), eew.Hypocenter?.OccurrenceTime);
	}
}
