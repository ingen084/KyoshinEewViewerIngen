using Avalonia.Data.Converters;
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
				ReportClassification.TrainingOrTest => "訓練・試験",
				_ => "不明"
			},
			"InformationType" => value switch
			{
				InformationType.Issue => "発表",
				InformationType.Cancellation => "取消",
				InformationType.Correction => "訂正",
				_ => "-",
			},
			"EewSeismicIntensity" => value switch
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
			},
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
				127 => "不明",
				101 => "巨大",
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
				0 => "津波なし",
				50 => "警報解除",
				51 => "津波警報",
				52 or 53 => "大津波警報",
				60 => "注意報解除",
				62 => "津波注意報",
				71 or 72 or 73 => "津波予報",
				_ => $"その他({value})",
			},
			"ReferenceTimeType" => value switch
			{
				ReferenceTimeType.Analysis => "実況",
				ReferenceTimeType.Estimate => "推定",
				ReferenceTimeType.Forecast => "予報",
				_ => "情報",
			},
			_ => throw new NotImplementedException($"不明な targetType {targetType}")
		};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
