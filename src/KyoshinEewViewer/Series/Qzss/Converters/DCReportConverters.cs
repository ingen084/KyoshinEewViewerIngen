using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinMonitorLib;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Series.Qzss.Converters;

public class DCReportConverters : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> parameter switch
		{
			"ReportClassification" => value switch
			{
				ReportClassification.Maximum => "最優先",
				ReportClassification.Priority => "優先",
				ReportClassification.Regular => "通常",
				ReportClassification.TrainingOrTest => "訓･試",
				_ => $"不明({value})"
			},
			"InformationType" => value switch
			{
				InformationType.Issue => "発表",
				InformationType.Cancellation => "取消",
				InformationType.Correction => "訂正",
				_ => "-",
			},
			"EewSeismicIntensity" => EewSeismicIntensityToJmaIntensity(value),
			"SeismicIntensity" => value switch
			{
				SeismicIntensity.Int4 => JmaIntensity.Int4,
				SeismicIntensity.Int5Lower => JmaIntensity.Int5Lower,
				SeismicIntensity.Int5Upper => JmaIntensity.Int5Upper,
				SeismicIntensity.Int6Lower => JmaIntensity.Int6Lower,
				SeismicIntensity.Int6Upper => JmaIntensity.Int6Upper,
				SeismicIntensity.Int7 => JmaIntensity.Int7,
				_ => JmaIntensity.Unknown,
			},
			"Magnitude" => value switch
			{
				(byte)127 => "不明",
				(byte)101 => "巨大",
				byte m => $"M{m / 10.0:F1}",
				_ => throw new NotImplementedException($"不明な Magnitude {value}")
			},
			"InformationSerialCode" => value switch
			{
				InformationSerialCode.InvestigatingA => "調査中(M6.8以上の地震発生)",
				InformationSerialCode.InvestigatingB => "調査中(みずみ計観測)",
				InformationSerialCode.InvestigatingC => "調査中(その他の事象観測)",
				InformationSerialCode.HugeEarthquakeWarning => "巨大地震警戒",
				InformationSerialCode.HugeEarthquakeCaution => "巨大地震注意",
				InformationSerialCode.InvestigateEnded => "調査終了",
				_ => "その他"
			},
			"TsunamiWarningCode" => value switch
			{
				(byte)1 => "津波なし",
				(byte)2 => "警報解除",
				(byte)3 => "津波警報",
				(byte)4 or (byte)5 => "大津波警報",
				(byte)15 => "その他の警報",
				_ => $"その他({value})",
			},
			"ReferenceTimeType" => value switch
			{
				ReferenceTimeType.Analysis => "実況",
				ReferenceTimeType.Estimate => "推定",
				ReferenceTimeType.Forecast => "予報",
				_ => "情報",
			},
			"Epicenter" => value switch {
				0 => "情報なし",
				int v => CsvDictionary.AreaEpicenter.TryGetValue(v, out var area) ? area : $"その他({v})",
				_ => "不明",
			},
			"Volcano" => value switch
			{
				int v => CsvDictionary.PointVolcano.TryGetValue(v, out var volcano) ? volcano : $"その他({v})",
				_ => "不明",
			},
			"VolcanicWarningCode" => value switch
			{
				(byte)127 => "その他",
				byte v => CsvDictionary.VolcanicWarning.TryGetValue(v, out var volcanicWarning) ? volcanicWarning : $"その他({v})",
				_ => "不明",
			},
			"Marine" => value switch
			{
				10000 => "その他",
				int v => CsvDictionary.AreaMarineJ.TryGetValue(v, out var marine) ? marine : $"その他({v})",
				_ => "不明",
			},
			"MarineWarningCode" => value switch
			{
				(byte)31 => "その他",
				byte v => CsvDictionary.MarineWarning.TryGetValue(v, out var marineWarning) ? marineWarning : $"その他({v})",
				_ => "不明",
			},
			"EewSeismicIntensityToForegroundColor" => GetColorFromIntensity(EewSeismicIntensityToJmaIntensity(value), true),
			"EewSeismicIntensityToBackgroundColor" => GetColorFromIntensity(EewSeismicIntensityToJmaIntensity(value), false),
			"IntensityToBackgroundColor" => GetColorFromIntensity(value is JmaIntensity i ? i : JmaIntensity.Error, false),
			"IntensityToForegroundColor" => GetColorFromIntensity(value is JmaIntensity i ? i : JmaIntensity.Error, true),
			"TsunamiArea" => value switch
			{
				990 => "予約済み",
				1000 => "その他",
				int v => CsvDictionary.AreaTsunami.TryGetValue(v, out var name) ? name : $"不明({v})",
				_ => "不明",
			},
			_ => throw new NotImplementedException($"不明な targetType {targetType}")
		};
	private JmaIntensity EewSeismicIntensityToJmaIntensity(object? value)
		=> value switch
		{
			EewSeismicIntensity.Int0 => JmaIntensity.Int0,
			EewSeismicIntensity.Int1 => JmaIntensity.Int1,
			EewSeismicIntensity.Int2 => JmaIntensity.Int2,
			EewSeismicIntensity.Int3 => JmaIntensity.Int3,
			EewSeismicIntensity.Int4 => JmaIntensity.Int4,
			EewSeismicIntensity.Int5Lower => JmaIntensity.Int5Lower,
			EewSeismicIntensity.Int5Upper => JmaIntensity.Int5Upper,
			EewSeismicIntensity.Int6Lower => JmaIntensity.Int6Lower,
			EewSeismicIntensity.Int6Upper => JmaIntensity.Int6Upper,
			EewSeismicIntensity.Int7 => JmaIntensity.Int7,
			_ => JmaIntensity.Unknown,
		};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();

	private static SolidColorBrush GetColorFromIntensity(JmaIntensity intensity, bool isForeground)
	{
		var attr = isForeground ? "Foreground" : "Background";
		return new SolidColorBrush((Color)(KyoshinEewViewerApp.Application?.FindResource($"{intensity}{attr}") ?? throw new NullReferenceException("震度色リソースを取得できません")));

	}
}
