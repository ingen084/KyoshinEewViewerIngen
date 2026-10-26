using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using EewModel = KyoshinEewViewer.Series.KyoshinMonitor.Models.Eew;
using EewHypocenterModel = KyoshinEewViewer.Series.KyoshinMonitor.Models.EewHypocenter;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

/// <summary>
/// AXIS の EEW 電文を EewController へ渡す
/// </summary>
internal class AxisEewReceiver
{
	private ILogger Logger { get; }
	private IEewController EewController { get; }

	public AxisEewReceiver(ILogger<AxisEewReceiver> logger, IEewController eewController)
	{
		Logger = logger;
		EewController = eewController;
	}

	public void Receive(EewMessage? message)
	{
		if (message == null)
		{
			LogInvalid("電文が空です", null);
			return;
		}
		if (message.Flag is not { } flag)
		{
			LogInvalid("Flag がありません", message.EventID);
			return;
		}
		if (flag.IsTraining)
			return;
		// ID が空のまま取消を流すと強震モニタ由来の EEW がすべて取消扱いになるため、ここで弾く
		if (string.IsNullOrWhiteSpace(message.EventID))
		{
			LogInvalid("EventID がありません", null);
			return;
		}

		var eventId = message.EventID;
		var reportTime = ToJstDateTime(message.ReportDateTime);
		if (flag.IsCancel)
		{
			Logger.LogInformation("AXIS からEEW取消報を受信しました: {EventId}", eventId);
			EewController.Cancelled(eventId, reportTime);
			return;
		}

		if (CreateEew(message, eventId, flag, reportTime) is { } eew)
			EewController.Update(eew, reportTime);
	}

	private EewModel? CreateEew(EewMessage message, string eventId, EewFlag flag, DateTime reportTime)
	{
		if (message.Hypocenter is not { } hypocenter)
		{
			LogInvalid("Hypocenter がありません", eventId);
			return null;
		}
		if (ResolveDepth(hypocenter.Depth) is not { } depth)
		{
			LogInvalid("Hypocenter.Depth を解釈できません", eventId);
			return null;
		}

		var magnitude = ResolveMagnitude(message.Magnitude);
		var maxIntensity = ResolveIntensity(message.Intensity);
		// Error は数値上 Int7 より大きく最大震度として選ばれてしまうため、Unknown と合わせて除外する
		var forecasts = (message.Forecast ?? [])
			.Select(f => (f.Code, Intensity: ResolveForecastIntensity(f.Intensity), IsOver: IsIntensityOver(f.Intensity)))
			.Where(f => IsValidIntensity(f.Intensity))
			.ToArray();

		// Text の文言だけでは警報状態が安定しないため、警報の発表基準である予想最大震度5弱以上でも警報とみなす
		var isWarning = (message.Title?.Contains("警報", StringComparison.Ordinal) ?? false) ||
						(message.Text?.Contains("強い揺れ", StringComparison.Ordinal) ?? false) ||
						(IsValidIntensity(maxIntensity) && maxIntensity >= JmaIntensity.Int5Lower);

		return new EewModel
		{
			Id = eventId,
			Source = EewSource.Axis,
			DisplaySource = "AXIS",
			ReceiveTime = reportTime,
			SerialNo = message.Serial,
			IsFinal = flag.IsFinal,
			MaxIntensity = maxIntensity,
			// 電文全体の「以上」は持たないため、最大震度と同じ予想震度の地域が「以上」かどうかで判定する
			IsIntensityOver = forecasts.Any(f => f.IsOver && f.Intensity == maxIntensity),
			Hypocenter = new EewHypocenterModel
			{
				OccurrenceTime = ToJstDateTime(message.OriginDateTime),
				Place = hypocenter.Name,
				Location = hypocenter.Coordinate is { Length: >= 2 } coordinate
					? new Location(coordinate[1], coordinate[0])
					: null,
				Magnitude = magnitude,
				Depth = depth,
				// AXIS の EEW には仮定震源要素のフラグがないため、PLUM 法の初期値を目安に判定する。
				IsTemporary = depth == 10 && magnitude is { } m && Math.Abs(m - 1.0f) < 0.01f,
			},
			IntensityForecastMap = forecasts.Length > 0
				? forecasts.GroupBy(f => f.Code).ToDictionary(g => g.Key, g => g.Max(f => f.Intensity))
				: null,
			WarningAreas = null,
			IsWarning = isWarning,
		};
	}

	private void LogInvalid(string reason, string? eventId)
		=> Logger.LogWarning("AXIS のEEWに必要な情報が欠けているため処理しませんでした: {Reason} {EventId}", reason, eventId);

	// アプリ内の時刻は日本標準時の壁時計で統一しているため、マシンのタイムゾーンに依存しないよう明示的に変換する
	private static DateTime ToJstDateTime(DateTimeOffset value)
		=> value.ToOffset(TimeSpan.FromHours(9)).DateTime;

	private static int? ResolveDepth(string? depth)
	{
		if (string.IsNullOrWhiteSpace(depth))
			return null;

		var value = depth;
		if (value.EndsWith("km", StringComparison.OrdinalIgnoreCase))
			value = value[..^2];

		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
	}

	private static float? ResolveMagnitude(string? magnitude)
		=> float.TryParse(magnitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

	private static JmaIntensity ResolveIntensity(string? intensity)
		=> string.IsNullOrWhiteSpace(intensity) ? JmaIntensity.Unknown : intensity.ToJmaIntensity();

	private static bool IsValidIntensity(JmaIntensity intensity)
		=> intensity is >= JmaIntensity.Int0 and <= JmaIntensity.Int7;

	private static bool IsIntensityOver(EewIntensity? intensity)
		=> string.Equals(intensity?.To, "over", StringComparison.OrdinalIgnoreCase);

	private static JmaIntensity ResolveForecastIntensity(EewIntensity? intensity)
	{
		if (intensity == null)
			return JmaIntensity.Unknown;
		if (IsIntensityOver(intensity))
			return ResolveIntensity(intensity.From);

		var to = ResolveIntensity(intensity.To);
		return to != JmaIntensity.Unknown ? to : ResolveIntensity(intensity.From);
	}
}
