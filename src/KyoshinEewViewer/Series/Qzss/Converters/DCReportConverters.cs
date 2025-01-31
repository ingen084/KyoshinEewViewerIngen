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
				int v => CsvDictionary.AreaTsunami.TryGetValue(v, out var name) ? name : $"その他({v})",
				_ => "不明",
			},
			"WeatherDisasterSubCategory" => value switch
			{
				(byte)1 => "暴風雪特別警報",
				(byte)2 => "大雨特別警報",
				(byte)3 => "暴風特別警報",
				(byte)4 => "大雪特別警報",
				(byte)5 => "波浪特別警報",
				(byte)6 => "高潮特別警報",
				(byte)7 => "全ての気象特別警報",
				(byte)21 => "記録的短時間大雨情報",
				(byte)22 => "竜巻注意情報",
				(byte)23 => "土砂災害警戒情報",
				(byte)31 => "その他の警報等情報要素",
				byte b => $"その他({b})",
				_ => "不明",
			},
			"AreaForecastLocalM" => value switch
			{
				500000 => "その他",
				int v => CsvDictionary.AreaForecastLocalM.TryGetValue(v, out var name) ? name : $"その他({v})",
				_ => "不明",
			},
			"TsunamigenicPotential" => value switch
			{
				(byte)0 => "津波の心配なし",
				(byte)1 => "破壊的な広域津波が発生する可能性あり",
				(byte)2 => "破壊的な地域津波が発生する可能性あり",
				(byte)3 => "震源付近で破壊的な津波が発生する可能性あり",
				(byte)4 => "破壊的な局地津波が発生する可能性は極めて低い",
				byte b => $"その他({b})",
				_ => "不明",
			},
			"NPTCoastalRegionArea" => value switch
			{
				(byte)1 or (byte)2 or (byte)66 or (byte)67 => "カムチャツカ半島東海岸",
				(byte)3 or (byte)4 => "千島列島",
				(byte)5 or (byte)6 or (byte)7 or (>= (byte)68 and <= 70) => "韓国南海岸",
				(byte)8 or (>= (byte)71 and <= 74) => "台湾",
				>= (byte)9 and <= 14 => "フィリピン東海岸",
				(>= (byte)15 and <= 20) or (byte)75 => "イリアンジャヤ北海岸",
				(>= (byte)21 and <= 28) or (byte)76 => "パプアニューギニア北海岸",
				(byte)29 or (byte)30 => "マリアナ諸島",
				(byte)31 => "パラオ",
				>= (byte)32 and <= 35 => "ミクロネシア",
				(byte)36 => "マーシャル諸島",
				(byte)37 or (byte)38 or (byte)39 or (byte)77 => "ソロモン諸島北海岸",
				(byte)40 or (byte)41 or (>= (byte)78 and <= 82) => "ソロモン海",
				(byte)83 => "珊瑚海",
				>= (byte)84 and <= 86 => "東シナ海沿岸",
				byte b => b.ToString(),
				_ => "不明",
			},
			"NPTCoastalRegion" => value switch
			{
				(byte)1 => "ウスト・カムチャツク",
				(byte)2 => "ペトロパブロフスク・カムチャツキー",
				(byte)3 => "セベロ・クリリスク",
				(byte)4 => "ウルップ島",
				(byte)5 => "釜山",
				(byte)6 => "蘆花島",
				(byte)7 => "西帰浦",
				(byte)8 => "花蓮",
				(byte)9 => "バスコ",
				(byte)10 => "パラナン",
				(byte)11 => "レガスピ",
				(byte)12 => "ラオアン",
				(byte)13 => "マドリッド",
				(byte)14 => "ダバオ",
				(byte)15 => "ベレベレ",
				(byte)16 => "パタニ",
				(byte)17 => "ソロン",
				(byte)18 => "マノクワリ",
				(byte)19 => "ワルサ",
				(byte)20 => "ジャヤプラ",
				(byte)21 => "バニモ",
				(byte)22 => "ウェワク",
				(byte)23 => "マダン",
				(byte)24 => "マヌス諸島",
				(byte)25 => "ラバウル",
				(byte)26 => "カビエン",
				(byte)27 => "キンベ",
				(byte)28 => "キエタ",
				(byte)29 => "グアム",
				(byte)30 => "サイパン",
				(byte)31 => "マラカル",
				(byte)32 => "ヤップ島",
				(byte)33 => "チューク島",
				(byte)34 => "ポンペイ島",
				(byte)35 => "コスラエ島",
				(byte)36 => "エニウェトク島",
				(byte)37 => "パンゴー",
				(byte)38 => "アウキ",
				(byte)39 => "キラキラ",
				(byte)40 => "ムンダ",
				(byte)41 => "ホニアラ",
				(byte)73 or (>= (byte)42 and <= 65) or (>= (byte)87 and <= 96) => $"予約済み({value})",
				(byte)66 => "カラギンスキー島",
				(byte)67 => "ニコリスコエ",
				(byte)68 => "統営",
				(byte)69 => "黒山島",
				(byte)70 => "済州島",
				(byte)71 => "基隆",
				(byte)72 => "台東",
				(byte)74 => "ホメル",
				(byte)75 => "ゲメ",
				(byte)76 => "ウラモナ",
				(byte)77 => "ガテレ",
				(byte)78 => "アムン",
				(byte)79 => "ファラマエ",
				(byte)80 => "ミシマ島",
				(byte)81 => "アロタウ",
				(byte)82 => "ラエ",
				(byte)83 => "ポートモレスビー",
				(byte)84 => "上海",
				(byte)85 => "舟山",
				(byte)86 => "温州",
				(byte)99 => "不明(99)",
				byte b => $"その他({b})",
				_ => "不明",
			},
			"AreaInformationCity" => value switch 
			{
				199999 => "北海道その他市町村",
				299999 => "青森県その他市町村",
				399999 => "岩手県その他市町村",
				499999 => "宮城県その他市町村",
				599999 => "秋田県その他市町村",
				699999 => "山形県その他市町村",
				799999 => "福島県その他市町村",
				899999 => "茨城県その他市町村",
				999999 => "栃木県その他市町村",
				1099999 => "群馬県その他市町村",
				1199999 => "埼玉県その他市町村",
				1299999 => "千葉県その他市町村",
				1399999 => "東京都その他市町村",
				1499999 => "神奈川県その他市町村",
				1599999 => "新潟県その他市町村",
				1699999 => "富山県その他市町村",
				1799999 => "石川県その他市町村",
				1899999 => "福井県その他市町村",
				1999999 => "山梨県その他市町村",
				2099999 => "長野県その他市町村",
				2199999 => "岐阜県その他市町村",
				2299999 => "静岡県その他市町村",
				2399999 => "愛知県その他市町村",
				2499999 => "三重県その他市町村",
				2599999 => "滋賀県その他市町村",
				2699999 => "京都府その他市町村",
				2799999 => "大阪府その他市町村",
				2899999 => "兵庫県その他市町村",
				2999999 => "奈良県その他市町村",
				3099999 => "和歌山県その他市町村",
				3199999 => "鳥取県その他市町村",
				3299999 => "島根県その他市町村",
				3399999 => "岡山県その他市町村",
				3499999 => "広島県その他市町村",
				3599999 => "山口県その他市町村",
				3699999 => "徳島県その他市町村",
				3799999 => "香川県その他市町村",
				3899999 => "愛媛県その他市町村",
				3999999 => "高知県その他市町村",
				4099999 => "福岡県その他市町村",
				4199999 => "佐賀県その他市町村",
				4299999 => "長崎県その他市町村",
				4399999 => "熊本県その他市町村",
				4499999 => "大分県その他市町村",
				4599999 => "宮崎県その他市町村",
				4699999 => "鹿児島県その他市町村",
				4799999 => "沖縄県その他市町村",
				int i => CsvDictionary.AreaInformationCity.TryGetValue(i, out var name) ? name : $"その他({i})",
				_ => "不明",
			},
			"AshFallWarningType" => value switch
			{
				(byte)1 => "速報",
				(byte)2 => "詳細",
				byte b => $"その他({b})",
				_ => "不明",
			},
			"AshFallWarningCode" => value switch
			{
				(byte)1 => "少量の降灰",
				(byte)2 => "やや多量の降灰",
				(byte)3 => "多量の降灰",
				(byte)4 => "小さな噴石の落下",
				byte b => $"その他({b})",
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
