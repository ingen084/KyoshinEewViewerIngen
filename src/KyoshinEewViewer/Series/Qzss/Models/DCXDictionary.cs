using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

/// <summary>
/// 災危通報(拡張) のコードに対応する表示用の文字列
/// </summary>
public static class DCXDictionary
{
	/// <summary>
	/// 情報の種別名
	/// </summary>
	public static string GetKindName(DCXReport report) => report switch
	{
		LAlertDCXReport => "Lアラート",
		JAlertDCXReport => "Jアラート",
		LocalGovernmentDCXReport => "自治体発信情報",
		OverseasDCXReport => "国外機関の情報",
		_ => "災危通報(拡張)",
	};

	/// <summary>
	/// 情報の種別名 (一覧表示用の短い名前)
	/// </summary>
	public static string GetKindShortName(DCXReport report) => report switch
	{
		LAlertDCXReport => "Lアラート",
		JAlertDCXReport => "Jアラート",
		LocalGovernmentDCXReport => "自治体",
		OverseasDCXReport => "国外",
		_ => "DCX",
	};

	/// <summary>
	/// A1 メッセージ種別
	/// </summary>
	public static string GetMessageTypeName(AlertMessageType type) => type switch
	{
		AlertMessageType.Test => "試験",
		AlertMessageType.Alert => "発表",
		AlertMessageType.Update => "更新",
		AlertMessageType.AllClear => "解除",
		_ => $"不明({(int)type})",
	};

	/// <summary>
	/// A5 深刻度
	/// </summary>
	public static string GetSeverityName(AlertSeverity severity) => severity switch
	{
		AlertSeverity.Unknown => "不明",
		AlertSeverity.Moderate => "中程度",
		AlertSeverity.Severe => "重大",
		AlertSeverity.Extreme => "極めて重大",
		_ => $"不明({(int)severity})",
	};

	/// <summary>
	/// A5 深刻度の説明
	/// </summary>
	public static string GetSeverityDescription(AlertSeverity severity) => severity switch
	{
		AlertSeverity.Moderate => "生命や財産への脅威の可能性",
		AlertSeverity.Severe => "生命や財産への重大な脅威",
		AlertSeverity.Extreme => "生命や財産への極めて重大な脅威",
		_ => "不明",
	};

	/// <summary>
	/// A8 継続時間
	/// </summary>
	public static string GetHazardDurationName(HazardDuration duration) => duration switch
	{
		HazardDuration.LessThan6Hours => "6時間未満",
		HazardDuration.From6To12Hours => "6時間以上12時間未満",
		HazardDuration.From12To24Hours => "12時間以上24時間未満",
		_ => "不明",
	};

	/// <summary>
	/// EX2 避難方向
	/// </summary>
	public static string GetEvacuateDirectionText(EvacuateDirectionType direction) => direction switch
	{
		EvacuateDirectionType.Leave => "追加対象地域の範囲から離れてください。",
		EvacuateDirectionType.Head => "追加対象地域の範囲に向かってください。",
		_ => $"不明({(int)direction})",
	};

	#region 国・地域、提供機関

	// CAMF Annex C 2. A2 - Country / Region Name Field (0〜248)
	private static readonly string[] CountryRegionNames = [
		"Afghanistan", "Albania", "Antarctica", "Algeria", "American Samoa", "Andorra", "Angola", "Antigua and Barbuda", "Azerbaijan", "Argentina",
		"Australia", "Austria", "Bahamas (the)", "Bahrain", "Bangladesh", "Armenia", "Barbados", "Belgium", "Bermuda", "Bhutan",
		"Bolivia (Plurinational State of)", "Bosnia and Herzegovina", "Botswana", "Bouvet Island", "Brazil", "Belize",
		"British Indian Ocean Territory (the)", "Solomon Islands", "Virgin Islands (British)", "Brunei Darussalam", "Bulgaria", "Myanmar", "Burundi",
		"Belarus", "Cambodia", "Cameroon", "Canada", "Cabo Verde", "Cayman Islands (the)", "Central African Republic (the)", "Sri Lanka", "Chad", "Chile",
		"China", "Taiwan (Province of China)", "Christmas Island", "Cocos (Keeling) Islands (the)", "Colombia", "Comoros (the)", "Mayotte", "Congo (the)",
		"Congo (the Democratic Republic of the)", "Cook Islands (the)", "Costa Rica", "Croatia", "Cuba", "Cyprus", "Czechia", "Benin", "Denmark",
		"Dominica", "Dominican Republic (the)", "Ecuador", "El Salvador", "Equatorial Guinea", "Ethiopia", "Eritrea", "Estonia", "Faroe Islands (the)",
		"Falkland Islands (the) [Malvinas]", "South Georgia and the South Sandwich Islands", "Fiji", "Finland", "Åland Islands", "France", "French Guiana",
		"French Polynesia", "French Southern Territories (the)", "Djibouti", "Gabon", "Georgia", "Gambia (the)", "Palestine, State of", "Germany", "Ghana",
		"Gibraltar", "Kiribati", "Greece", "Greenland", "Grenada", "Guadeloupe", "Guam", "Guatemala", "Guinea", "Guyana", "Haiti",
		"Heard Island and McDonald Islands", "Holy See (the)", "Honduras", "Hong Kong", "Hungary", "Iceland", "India", "Indonesia",
		"Iran (Islamic Republic of)", "Iraq", "Ireland", "Israel", "Italy", "Côte d'Ivoire", "Jamaica", "Japan", "Kazakhstan", "Jordan", "Kenya",
		"Korea (the Democratic People's Republic of)", "Korea (the Republic of)", "Kuwait", "Kyrgyzstan", "Lao People's Democratic Republic (the)",
		"Lebanon", "Lesotho", "Latvia", "Liberia", "Libya", "Liechtenstein", "Lithuania", "Luxembourg", "Macao", "Madagascar", "Malawi", "Malaysia",
		"Maldives", "Mali", "Malta", "Martinique", "Mauritania", "Mauritius", "Mexico", "Monaco", "Mongolia", "Moldova (the Republic of)", "Montenegro",
		"Montserrat", "Morocco", "Mozambique", "Oman", "Namibia", "Nauru", "Nepal", "Netherlands (the)", "Curaçao", "Aruba", "Sint Maarten (Dutch part)",
		"Bonaire, Sint Eustatius and Saba", "New Caledonia", "Vanuatu", "New Zealand", "Nicaragua", "Niger (the)", "Nigeria", "Niue", "Norfolk Island",
		"Norway", "Northern Mariana Islands (the)", "United States Minor Outlying Islands (the)", "Micronesia (Federated States of)",
		"Marshall Islands (the)", "Palau", "Pakistan", "Panama", "Papua New Guinea", "Paraguay", "Peru", "Philippines (the)", "Pitcairn", "Poland",
		"Portugal", "Guinea-Bissau", "Timor-Leste", "Puerto Rico", "Qatar", "Réunion", "Romania", "Russian Federation (the)", "Rwanda", "Saint Barthélemy",
		"Saint Helena, Ascension and Tristan da Cunha", "Saint Kitts and Nevis", "Anguilla", "Saint Lucia", "Saint Martin (French part)",
		"Saint Pierre and Miquelon", "Saint Vincent and the Grenadines", "San Marino", "Sao Tome and Principe", "Saudi Arabia", "Senegal", "Serbia",
		"Seychelles", "Sierra Leone", "Singapore", "Slovakia", "Viet Nam", "Slovenia", "Somalia", "South Africa", "Zimbabwe", "Spain", "South Sudan",
		"Sudan (the)", "Western Sahara*", "Suriname", "Svalbard and Jan Mayen", "Eswatini", "Sweden", "Switzerland", "Syrian Arab Republic (the)",
		"Tajikistan", "Thailand", "Togo", "Tokelau", "Tonga", "Trinidad and Tobago", "United Arab Emirates (the)", "Tunisia", "Turkey", "Turkmenistan",
		"Turks and Caicos Islands (the)", "Tuvalu", "Uganda", "Ukraine", "North Macedonia", "Egypt",
		"United Kingdom of Great Britain and Northern Ireland (the)", "Guernsey", "Jersey", "Isle of Man", "Tanzania, the United Republic of",
		"United States of America (the)", "Virgin Islands (U.S.)", "Burkina Faso", "Uruguay", "Uzbekistan", "Venezuela (Bolivarian Republic of)",
		"Wallis and Futuna", "Samoa", "Yemen", "Zambia",
	];

	private const ushort AustraliaCountryRegionCode = 10;
	private const ushort FijiCountryRegionCode = 71;
	private const ushort ThailandCountryRegionCode = 219;

	/// <summary>
	/// A2 国・地域名<br/>
	/// 提供機関の定義がある国・地域は日本語、それ以外は CAMF の英語表記
	/// </summary>
	public static string GetCountryRegionName(ushort code) => code switch
	{
		DCXReport.JapanCountryRegionCode => "日本",
		AustraliaCountryRegionCode => "オーストラリア",
		FijiCountryRegionCode => "フィジー",
		ThailandCountryRegionCode => "タイ",
		500 => "EU の機関",
		501 => "国連の機関",
		502 => "国際機関",
		_ when code < CountryRegionNames.Length => CountryRegionNames[code],
		_ => $"不明({code})",
	};

	// 仕様書 表4.2-6 (1〜)
	private static readonly string[] JapanProviderNames = ["マルチメディア振興センター", "消防庁", "関係省庁", "地方公共団体"];
	private static readonly string[] AustraliaProviderNames = [
		"National Emergency Management Agency (NEMA)", "Bureau of Meteorology (BOM)", "Australian Climate Service (ACS)", "Geoscience Australia (GA)",
		"Commonwealth Scientific and Industrial Research Organisation (CSIRO)", "Australian Bureau of Statistics (ABS)",
		"Resilience New South Wales (Resilience NSW)", "State Emergency Service New South Wales (SES)", "New South Wales Rural Fire Service (NSW RFS)",
		"Joint Australian Tsunami Warning Centre (JATWC)", "Flood Knowledge Centre (FKC)", "Australian Broadcasting Corporation (ABC)",
	];
	private static readonly string[] FijiProviderNames = [
		"National Disaster Management Office (NDMO)", "Fiji Meteorological Service (FMS)", "Hydrology Section, Fiji Water Authority (FWA)",
		"Mineral Resources Department (MRD)", "Fiji Broadcasting Corporation (FBC)",
	];
	private static readonly string[] ThailandProviderNames = [
		"Department of Disaster Prevention and Mitigation (DDPM)", "Thai Meteorological Department (TMD)", "National Disaster Warning Center (NDWC)",
		"Department of Mineral Resources (DMR)", "Navy Hydrographic Department, Royal Thai Navy (RTN)", "Department of Water Resources (DWR)",
		"Royal Irrigation Department (RID)", "Department of Pollution Control (DPC)", "Geo-Informatics and Space Technology Development Agency (GISTDA)",
		"Electricity Generating Authority of Thailand (EGAT)", "Royal Forest Department (RFD)", "Department of Parks, Wildlife and Plant Conservation (DPWPC)",
		"Water Crisis Prevention Center (WCPC)",
	];

	/// <summary>
	/// A3 提供機関名 (国・地域によって定義が異なる)
	/// </summary>
	public static string GetProviderName(ushort countryRegionCode, byte providerIdentifier)
	{
		var names = countryRegionCode switch
		{
			DCXReport.JapanCountryRegionCode => JapanProviderNames,
			AustraliaCountryRegionCode => AustraliaProviderNames,
			FijiCountryRegionCode => FijiProviderNames,
			ThailandCountryRegionCode => ThailandProviderNames,
			_ => [],
		};
		return providerIdentifier >= 1 && providerIdentifier <= names.Length ? names[providerIdentifier - 1] : $"不明({providerIdentifier})";
	}

	#endregion

	#region 災害のカテゴリと種別

	// CAMF Annex C 4. A4 - Hazard Category and Type (1〜113)。公式の日本語訳がないため独自に訳している
	private static readonly (string Category, string Type)[] Hazards = [
		("CBRNE", "航空攻撃"),
		("CBRNE", "サイバー攻撃"),
		("CBRNE", "核攻撃"),
		("CBRNE", "生物剤による危険"),
		("CBRNE", "化学物質による危険"),
		("CBRNE", "爆発物による危険"),
		("CBRNE", "隕石の落下"),
		("CBRNE", "ミサイル攻撃"),
		("CBRNE", "核物質による危険"),
		("CBRNE", "原子力発電所の事故"),
		("CBRNE", "放射性物質による危険"),
		("CBRNE", "人工衛星等の落下"),
		("CBRNE", "サイレンの試験"),
		("環境", "酸性雨"),
		("環境", "大気汚染"),
		("環境", "飲料水の汚染"),
		("環境", "ガス漏れ"),
		("環境", "海洋汚染"),
		("環境", "騒音"),
		("環境", "虫の大量発生"),
		("環境", "河川の汚染"),
		("環境", "浮遊粉じん"),
		("環境", "紫外線"),
		("火災", "大規模火災"),
		("火災", "消防隊の出動"),
		("火災", "火災による有毒ガス"),
		("火災", "林野火災"),
		("火災", "煙"),
		("火災", "悪臭"),
		("火災", "火災の危険"),
		("火災", "建物火災・工場火災"),
		("地象", "降灰"),
		("地象", "なだれの危険"),
		("地象", "地割れ・陥没"),
		("地象", "土石流"),
		("地象", "地震"),
		("地象", "磁気嵐・太陽嵐"),
		("地象", "氷河の崩落"),
		("地象", "土砂崩れ"),
		("地象", "溶岩流"),
		("地象", "火砕流"),
		("地象", "吹きだまり"),
		("地象", "潮汐波"),
		("地象", "津波"),
		("地象", "火山泥流"),
		("地象", "噴火"),
		("地象", "高潮"),
		("保健", "家畜伝染病"),
		("保健", "食品の安全に関する警報"),
		("保健", "健康被害のおそれ"),
		("保健", "パンデミック"),
		("保健", "病害虫の発生"),
		("保健", "感染の危険"),
		("インフラ", "建物の倒壊"),
		("インフラ", "緊急通報番号の障害"),
		("インフラ", "ガスの供給停止"),
		("インフラ", "ITシステムの障害"),
		("インフラ", "停電"),
		("インフラ", "未処理下水の流出"),
		("インフラ", "電話回線の障害"),
		("気象", "路面凍結"),
		("気象", "沿岸部の浸水"),
		("気象", "寒波"),
		("気象", "広範囲の突風"),
		("気象", "干ばつ"),
		("気象", "砂じん嵐"),
		("気象", "流氷・氷山"),
		("気象", "洪水"),
		("気象", "濃霧"),
		("気象", "ひょう"),
		("気象", "熱波"),
		("気象", "雷"),
		("気象", "花粉"),
		("気象", "大雨"),
		("気象", "暴風雪"),
		("気象", "大雪"),
		("気象", "暴風・雷雨"),
		("気象", "凍土の融解"),
		("気象", "竜巻"),
		("気象", "ハリケーン"),
		("気象", "霜"),
		("気象", "台風"),
		("救助", "ダムの決壊"),
		("救助", "堤防の決壊"),
		("救助", "不発弾の処理"),
		("救助", "工場の事故"),
		("救助", "鉱山の災害"),
		("安全", "爆発物・弾薬の発見"),
		("安全", "デモ"),
		("安全", "危険物の事故"),
		("安全", "生命の危機"),
		("安全", "大規模イベント"),
		("安全", "行方不明・誘拐"),
		("安全", "爆発の危険"),
		("安全", "安全に関する警告"),
		("安全", "正体不明の飛行物体"),
		("安全", "正体不明の動物"),
		("治安", "化学攻撃"),
		("治安", "ゲリラ攻撃"),
		("治安", "ハイジャック"),
		("治安", "銃撃・武器による危険"),
		("治安", "特殊部隊による攻撃"),
		("治安", "テロ"),
		("交通", "航空機の墜落"),
		("交通", "橋の崩落"),
		("交通", "危険物の輸送事故"),
		("交通", "内陸水運の事故"),
		("交通", "海難・海上保安"),
		("交通", "油の流出"),
		("交通", "道路交通の障害"),
		("交通", "鉄道事故"),
		("交通", "トンネル内の事故"),
		("その他", "テスト"),
	];

	/// <summary>
	/// A4 災害の種別名
	/// </summary>
	public static string GetHazardName(byte code)
		=> code >= 1 && code <= Hazards.Length ? Hazards[code - 1].Type : $"不明({code})";

	/// <summary>
	/// A4 災害のカテゴリ名
	/// </summary>
	public static string GetHazardCategoryName(byte code)
		=> code >= 1 && code <= Hazards.Length ? Hazards[code - 1].Category : "不明";

	#endregion

	#region 避難行動指示

	private const string BestActionText = "直ちに命を守るための最善の行動を。";

	// 仕様書 表4.2-14 基本指示情報 (1〜7)
	private static readonly string[] BasicInstructionInformationTexts = [
		"頑丈なものの下/中。",
		"3階以上。",
		"地下。",
		"山。",
		"水場。",
		"工場等化学系を取扱う建物。",
		"崖等崩れやすい場所。",
	];

	/// <summary>
	/// A11 避難行動指示の文言<br/>
	/// 国内向けの電文は仕様書 表4.2-15 の文言、国外機関の電文はライブラリのコードを返す
	/// </summary>
	/// <returns>指示の文言。指示がない場合は null</returns>
	public static string? GetInstructionText(DCXReport report)
	{
		var code = report.CommonAlertMessage.InstructionCode;
		if (code == 0)
			return null;

		if (report is OverseasDCXReport)
			return report.CommonAlertMessage.LibraryType == LibraryType.International
				// 国際ライブラリは上位5ビットが List A、下位5ビットが List B
				? $"国際ライブラリ IC-A-{(code >> 5) + 1:00} / IC-B-{(code & 0b11111) + 1:00}"
				: $"国・地域ライブラリ ({code})";

		var basicInstruction = code >> 8;
		var information = code & 0xFF;
		if (basicInstruction == 0)
			return information switch
			{
				0x01 or 0x7F or 0xFF => BestActionText,
				0x7E => "これは、DCXのテストです。",
				0x80 => "ミサイル発射。ミサイル発射。ミサイルが発射されたものとみられます。建物の中、又は地下に避難して下さい。",
				0x81 => "ミサイル通過。ミサイル通過。先程のミサイルは通過したものとみられます。避難の呼びかけを解除します。不審なものには決して近寄らず直ちに警察や消防などに連絡して下さい。",
				0x82 => "先程のミサイルは、海に落下したものとみられます。避難の呼びかけを解除します。不審なものには決して近寄らず直ちに警察や消防などに連絡して下さい。",
				0x83 => "先程のミサイルは、我が国には飛来しないものとみられます。避難の呼びかけを解除します。",
				0x84 => "直ちに避難。直ちに避難。直ちに建物の中、又は地下に避難して下さい。ミサイルが、周辺に落下するものとみられます。直ちに避難して下さい。",
				0x85 => "先程のミサイルは、迎撃により破壊されました。ミサイルの破片の落下の可能性があります。続報を伝達しますので、引き続き屋内に避難して下さい。",
				0x86 => "ミサイル落下。ミサイル落下。ミサイルが、周辺に落下したものとみられます。続報を伝達しますので、引き続き屋内に避難して下さい。",
				0x87 => "先程のミサイルは、我が国には落下しないものとみられます。避難の呼びかけを解除します。",
				0x88 => "これは、Jアラートのテストです。",
				_ => $"不明な指示({code})",
			};

		var verb = basicInstruction switch
		{
			0b01 => "留まれ。",
			0b10 => "向かえ。",
			_ => "離れろ。",
		};
		if (information == 0)
			return verb;
		if (information <= BasicInstructionInformationTexts.Length)
			return verb + BasicInstructionInformationTexts[information - 1];
		return $"不明な指示({code})";
	}

	/// <summary>
	/// J アラートの避難行動指示の要約 (一覧表示用)
	/// </summary>
	/// <returns>要約。要約がない指示の場合は null</returns>
	public static string? GetJAlertInstructionSummary(ushort instructionCode) => instructionCode switch
	{
		0x80 => "ミサイル発射",
		0x81 => "ミサイル通過",
		0x82 => "海に落下",
		0x83 => "飛来しない",
		0x84 => "直ちに避難",
		0x85 => "迎撃・破壊",
		0x86 => "ミサイル落下",
		0x87 => "落下しない",
		0x88 => "テスト",
		_ => null,
	};

	#endregion

	#region 対象地域

	/// <summary>
	/// 都道府県名
	/// </summary>
	/// <param name="code">都道府県コード (JIS X 0401)</param>
	public static string GetPrefectureName(int code)
		=> CsvDictionary.AreaInformationPrefectureEarthquake.TryGetValue(code, out var name) ? name : $"不明({code})";

	/// <summary>
	/// 市区町村名
	/// </summary>
	/// <param name="code">市区町村コード (JIS X 0402 の検査数字を除いた5桁)</param>
	public static string GetMunicipalityName(ushort code)
	{
		if (!CsvDictionary.AreaInformationCity.TryGetValue(code * 100, out var city))
			return $"不明({code:00000})";
		// 政令指定都市の区は「〇〇市のうち〇〇区」の形の正式名称しか持たない
		return string.IsNullOrEmpty(city.Name) ? city.FullName.Replace("のうち", "") : city.Name;
	}

	// 政令指定都市のコード → 区のコード (地図の市町村レイヤーは区ごとに分かれているため)
	private static readonly Lazy<Dictionary<int, int[]>> WardCodes = new(() =>
	{
		var cityCodes = CsvDictionary.AreaInformationCity
			.Where(c => !c.Value.FullName.Contains("のうち"))
			.GroupBy(c => c.Value.FullName)
			.ToDictionary(g => g.Key, g => g.First().Key);
		return CsvDictionary.AreaInformationCity
			.Where(c => c.Value.FullName.Contains("のうち"))
			.Select(c => (Parent: cityCodes.GetValueOrDefault(c.Value.FullName[..c.Value.FullName.IndexOf("のうち", StringComparison.Ordinal)]), Code: c.Key))
			.Where(c => c.Parent != 0)
			.GroupBy(c => c.Parent)
			.ToDictionary(g => g.Key, g => g.Select(c => c.Code).ToArray());
	});

	/// <summary>
	/// 市区町村を地図上で示す市町村等の区域のコード (地震津波関係の市町村等)
	/// </summary>
	/// <param name="code">市区町村コード (JIS X 0402 の検査数字を除いた5桁)</param>
	public static IEnumerable<long> GetMunicipalityAreaCodes(ushort code)
	{
		var areaCode = code * 100;
		if (WardCodes.Value.TryGetValue(areaCode, out var wards))
			return wards.Select(w => (long)w);
		return [areaCode];
	}

	#endregion
}
