using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Series.Qzss.Models;

namespace KyoshinEewViewer.Tests.Qzss;

public class DCXDictionaryTests
{
	[Fact(DisplayName = "避難行動指示を仕様書の表4.2-15の文言に変換する")]
	public void InstructionText()
	{
		var samples = new (ushort Code, string? Expected)[]
		{
			(0b00_00000000, null),
			(0b00_00000001, "直ちに命を守るための最善の行動を。"),
			(0b00_01111110, "これは、DCXのテストです。"),
			(0b00_01111111, "直ちに命を守るための最善の行動を。"),
			(0b00_10000000, "ミサイル発射。ミサイル発射。ミサイルが発射されたものとみられます。建物の中、又は地下に避難して下さい。"),
			(0b00_10001000, "これは、Jアラートのテストです。"),
			(0b00_11111111, "直ちに命を守るための最善の行動を。"),
			(0b01_00000000, "留まれ。"),
			(0b01_00000001, "留まれ。頑丈なものの下/中。"),
			// 仕様書 5.3 の例
			(0b10_00000011, "向かえ。地下。"),
			(0b11_00000101, "離れろ。水場。"),
			(0b11_00000111, "離れろ。崖等崩れやすい場所。"),
			// 予約
			(0b00_00000010, "不明な指示(2)"),
			(0b01_00001000, "不明な指示(264)"),
		};
		foreach (var (code, expected) in samples)
			Assert.Equal(expected, DCXDictionary.GetInstructionText((DCXReport)new DCXTestMessage(1, AlertMessageType.Alert, 74).Instruction(code).Parse()));

		// 国外機関の情報は国際ライブラリのコードを示す
		var overseas = new DCXTestMessage(1, AlertMessageType.Alert, 74).Set(26, 9, 10).Set(66, 1, 0).Instruction(0b00001_00010).Parse();
		Assert.Equal("国際ライブラリ IC-A-02 / IC-B-03", DCXDictionary.GetInstructionText((DCXReport)overseas));
	}

	[Fact(DisplayName = "市区町村コードから政令指定都市の区を含む市区町村名と地図の区域を求める")]
	public void Municipality()
	{
		// 試験用データ配信で使用される市区町村
		var names = new (ushort Code, string Name)[]
		{
			(1101, "北海道札幌市中央区"),
			(13101, "東京都千代田区"),
			(27100, "大阪府大阪市"),
			(47201, "沖縄県那覇市"),
			(1202, "北海道函館市"),
			(13201, "東京都八王子市"),
			(27140, "大阪府堺市"),
			(47382, "沖縄県与那国町"),
		};
		foreach (var (code, name) in names)
			Assert.Equal(name, DCXDictionary.GetMunicipalityName(code));
		Assert.Equal("不明(65535)", DCXDictionary.GetMunicipalityName(65535));

		// 地図の市町村等の区域は政令指定都市を区ごとに分けている
		Assert.Equal([110100], DCXDictionary.GetMunicipalityAreaCodes(1101));
		Assert.Equal([1310100], DCXDictionary.GetMunicipalityAreaCodes(13101));
		var osakaWards = DCXDictionary.GetMunicipalityAreaCodes(27100).ToArray();
		Assert.Equal(24, osakaWards.Length);
		Assert.All(osakaWards, c => Assert.InRange(c, 2710200, 2712800));
		// 堺市の区は大阪市に含めない
		Assert.Equal(7, DCXDictionary.GetMunicipalityAreaCodes(27140).Count());
	}

	[Fact(DisplayName = "国・地域、提供機関、災害種別、都道府県の名称を求める")]
	public void Names()
	{
		Assert.Equal("日本", DCXDictionary.GetCountryRegionName(DCXReport.JapanCountryRegionCode));
		Assert.Equal("オーストラリア", DCXDictionary.GetCountryRegionName(10));
		Assert.Equal("Afghanistan", DCXDictionary.GetCountryRegionName(0));
		Assert.Equal("Zambia", DCXDictionary.GetCountryRegionName(248));
		Assert.Equal("不明(249)", DCXDictionary.GetCountryRegionName(249));
		Assert.Equal("国連の機関", DCXDictionary.GetCountryRegionName(501));

		Assert.Equal("消防庁", DCXDictionary.GetProviderName(DCXReport.JapanCountryRegionCode, 2));
		Assert.Equal("地方公共団体", DCXDictionary.GetProviderName(DCXReport.JapanCountryRegionCode, 4));
		Assert.Equal("不明(5)", DCXDictionary.GetProviderName(DCXReport.JapanCountryRegionCode, 5));
		Assert.Equal("Bureau of Meteorology (BOM)", DCXDictionary.GetProviderName(10, 2));
		Assert.Equal("Water Crisis Prevention Center (WCPC)", DCXDictionary.GetProviderName(219, 13));

		// 試験用データ配信で使用される災害種別
		Assert.Equal("ミサイル攻撃", DCXDictionary.GetHazardName(8));
		Assert.Equal("洪水", DCXDictionary.GetHazardName(68));
		Assert.Equal("大雨", DCXDictionary.GetHazardName(74));
		Assert.Equal("台風", DCXDictionary.GetHazardName(82));
		Assert.Equal("安全に関する警告", DCXDictionary.GetHazardName(95));
		Assert.Equal("テスト", DCXDictionary.GetHazardName(113));
		Assert.Equal("その他", DCXDictionary.GetHazardCategoryName(113));
		Assert.Equal("不明(114)", DCXDictionary.GetHazardName(114));

		Assert.Equal("北海道", DCXDictionary.GetPrefectureName(1));
		Assert.Equal("沖縄県", DCXDictionary.GetPrefectureName(47));
	}
}
