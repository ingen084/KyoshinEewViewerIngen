using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Tests;

public class DCXReportTest
{
	// 2024年12月3日の災危通報(拡張)試験用データ配信で受信された電文
	// from: https://github.com/SWITCHSCIENCE/QZSSDCX/blob/main/examples/decode_test/decode_test.ino (MIT License)
	private const string JAlertAllPrefectures = "C6B0600DE2BF88E9200000000000000000001FFFFFFFFFFFC0000011A1C84ED4";
	private const string JAlertMissileLaunched = "9AB0600DE21188EA208000000000000000001FFFFFFFFFFFC00000138C7533B1";
	private const string LAlertSapporoChuoWithoutEllipse = "C6B0440DE10208ADE00000000000000000000113400000000000001395E136D4";
	private const string LAlertSapporoChuoWithEllipse = "53B0440DE17E08CCE000BD36E47A359984BA011340000000000000106031CF66";
	private const string LAlertChiyodaWithEllipse = "9AB0600DE1A488D6E000B2C1E361A5190C460CCB4000000000000011B0C18CB1";
	private const string LAlertNahaWithEllipse = "9AB0600DE1DD88E4E000A545DACBB58D907E2E184000000000000012A0AA9B71";

	/// <summary>
	/// DCX メッセージの基本形 (国・地域、提供機関、Vn のみ設定)
	/// </summary>
	private static byte[] CreateBaseData(ushort countryRegionCode, byte providerIdentifier, byte version)
	{
		var data = new byte[32];
		data[0] = (byte)Preamble.PatternA;
		TestUtils.SetValue(data, 8, 6, 44);
		TestUtils.SetValue(data, 26, 9, countryRegionCode);
		TestUtils.SetValue(data, 35, 5, providerIdentifier);
		TestUtils.SetValue(data, 214, 6, version);
		return data;
	}

	private static DCReport ParseWithCorrectCrc(byte[] data)
	{
		TestUtils.SetCorrectCRC(data);
		return DCReport.Parse(data);
	}

	[Fact(DisplayName = "試験配信の J アラート電文から CAMF と対象都道府県を取得できる")]
	public void ParseJAlertSample()
	{
		var report = Assert.IsType<JAlertDCXReport>(DCReport.Parse(Convert.FromHexString(JAlertAllPrefectures)));
		var camf = report.CommonAlertMessage;

		Assert.Equal(44, report.MessageType);
		Assert.Equal(new SatelliteDesignation(SatelliteDesignationMaskType.ServiceType, 0b001100000), report.SatelliteDesignation);
		Assert.Equal(AlertMessageType.Test, camf.MessageType);
		Assert.True(report.IsTest);
		Assert.Equal(DCXReport.JapanCountryRegionCode, camf.CountryRegionCode);
		Assert.Equal(2, camf.ProviderIdentifier); // FDMA
		Assert.Equal(95, camf.HazardCategoryAndType); // SAFETY - Safety warning
		Assert.Equal(AlertSeverity.Extreme, camf.Severity);
		Assert.Equal(HazardDuration.Unknown, camf.HazardDuration);
		Assert.Null(camf.HazardDurationUpperBound);
		Assert.Equal(LibraryType.CountryRegion, camf.LibraryType);
		Assert.Equal(0, camf.LibraryVersion);
		Assert.Equal(0, camf.InstructionCode);
		Assert.Null(camf.Ellipse);
		Assert.Equal(1, report.Version);

		// 火曜日 14:00 UTC
		Assert.Equal(new DateTimeOffset(2024, 12, 3, 14, 0, 0, TimeSpan.Zero), camf.GetHazardOnsetTime(new DateTimeOffset(2024, 12, 3, 14, 0, 30, TimeSpan.Zero)));

		Assert.Equal(TargetAreaCodeListType.Prefecture, report.TargetAreaCodeListType);
		Assert.Equal(JAlertDCXReport.AllPrefecturesMask, report.PrefectureMask);
		Assert.Equal(Enumerable.Range(1, 47), report.PrefectureCodes);

		var missile = Assert.IsType<JAlertDCXReport>(DCReport.Parse(Convert.FromHexString(JAlertMissileLaunched)));
		Assert.Equal(8, missile.CommonAlertMessage.HazardCategoryAndType); // CBRNE - Missile attack
		Assert.Equal(0b00_10000000, missile.CommonAlertMessage.InstructionCode); // ミサイル発射
	}

	[Fact(DisplayName = "試験配信の J アラート電文の都道府県コードを正しい都道府県として取得できる")]
	public void ParseJAlertPrefectures()
	{
		// 試験用データ配信情報の詳細「表9 EX9-Target Area Code List の Prefecture code のパターン」に対応する
		var samples = new (string Sentence, int[] Prefectures)[]
		{
			("C6B0600DE2E208EE20880000000000000000000000000000400000130C9DA654", [1]),
			("9AB0600DE2E208EF2088000000000000000000000000001F800000113C0237B1", [2, 3, 4, 5, 6, 7]),
			("C6B0600DE2E208EF20880000000000000000000000000FE000000012A3C82914", [8, 9, 10, 11, 12, 13, 14]),
			("9AB0600DE2E208F02088000000000000000000000003F00000000010905F49F1", [15, 16, 17, 18, 19, 20]),
			("C6B0600DE2E208F0208800000000000000000000003C0000000000121BF87454", [21, 22, 23, 24]),
			("9AB0600DE2E208F12088000000000000000000000FC000000000001015A2A271", [25, 26, 27, 28, 29, 30]),
			("C6B0600DE2E208F1208800000000000000000001F000000000000011B864C094", [31, 32, 33, 34, 35]),
			("9AB0600DE2E208F220880000000000000000001E000000000000001386CA21B1", [36, 37, 38, 39]),
			("9AB0600DE2E208F320880000000000000000100000000000000000130FA51EF1", [47]),
		};

		foreach (var (sentence, prefectures) in samples)
		{
			var report = Assert.IsType<JAlertDCXReport>(DCReport.Parse(Convert.FromHexString(sentence)));
			Assert.Equal(113, report.CommonAlertMessage.HazardCategoryAndType); // OTHER - Test alert
			Assert.Equal(0b00_10001000, report.CommonAlertMessage.InstructionCode); // これは、Jアラートのテストです。
			Assert.Equal(prefectures, report.PrefectureCodes);
		}
	}

	[Fact(DisplayName = "試験配信の L アラート電文から対象市区町村と楕円を取得できる")]
	public void ParseLAlertSample()
	{
		var withoutEllipse = Assert.IsType<LAlertDCXReport>(DCReport.Parse(Convert.FromHexString(LAlertSapporoChuoWithoutEllipse)));
		Assert.Equal(1, withoutEllipse.CommonAlertMessage.ProviderIdentifier); // FMMC
		Assert.Equal(1, withoutEllipse.CommonAlertMessage.HazardCategoryAndType); // CBRNE - Air strike
		Assert.Equal(HazardDuration.From12To24Hours, withoutEllipse.CommonAlertMessage.HazardDuration);
		Assert.Equal(TimeSpan.FromHours(24), withoutEllipse.CommonAlertMessage.HazardDurationUpperBound);
		Assert.Equal(1101, withoutEllipse.TargetAreaCode); // 札幌市中央区
		Assert.Null(withoutEllipse.CommonAlertMessage.Ellipse);

		// 期待値は B1 (C1〜C4) の補正を含めて仕様書の式から別途計算した値
		var samples = new (string Sentence, ushort TargetAreaCode, DcxEllipse Ellipse)[]
		{
			(LAlertSapporoChuoWithEllipse, 1101, new(43.041314, 141.297961, 9.8647, 6.7683, 53.4375)),
			(LAlertChiyodaWithEllipse, 13101, new(35.689288, 139.756773, 2.8435, 2.1815, 50.625)),
			(LAlertNahaWithEllipse, 47201, new(26.208286, 127.682706, 8.4407, 6.2418, -14.0625)),
		};
		foreach (var (sentence, targetAreaCode, expected) in samples)
		{
			var report = Assert.IsType<LAlertDCXReport>(DCReport.Parse(Convert.FromHexString(sentence)));
			Assert.Equal(targetAreaCode, report.TargetAreaCode);
			Assert.Equal(SpecificSettingsType.ImprovedResolutionOfMainEllipse, report.CommonAlertMessage.SpecificSettingsType);
			var ellipse = Assert.NotNull(report.CommonAlertMessage.Ellipse);
			Assert.Equal(expected.Latitude, ellipse.Latitude, 0.000001);
			Assert.Equal(expected.Longitude, ellipse.Longitude, 0.000001);
			Assert.Equal(expected.SemiMajorAxis, ellipse.SemiMajorAxis, 0.0001);
			Assert.Equal(expected.SemiMinorAxis, ellipse.SemiMinorAxis, 0.0001);
			Assert.Equal(expected.Azimuth, ellipse.Azimuth, 0.000001);
		}
	}

	[Fact(DisplayName = "楕円パラメータの換算が仕様書の計算例・表と一致する")]
	public void EllipseConversion()
	{
		// 4.2.3.12〜4.2.3.16、4.2.4.1.3〜4.2.4.1.7 の計算例
		Assert.Equal(35.688, DcxEllipse.GetMainLatitude(45761), 0.0005);
		Assert.Equal(139.691, DcxEllipse.GetMainLongitude(116395), 0.0005);
		Assert.Equal(45.00, DcxEllipse.GetMainAzimuth(48), 0.005);
		Assert.Equal(35.687, DcxEllipse.GetAdditionalLatitude(91522), 0.0005);
		Assert.Equal(139.689, DcxEllipse.GetAdditionalLongitude(68950), 0.0005);
		Assert.Equal(45.00, DcxEllipse.GetAdditionalAzimuth(96), 0.005);
		// EX4 は 45〜225度を表すため、180度を超える値は西経になる
		Assert.Equal(-135, DcxEllipse.GetAdditionalLongitude((1 << 17) - 1), 0.000001);

		// 表 4.2-17 / 4.2-18
		var radius = new (int Code, double Radius)[] { (0, 0.216), (1, 0.292), (9, 3.269), (11, 5.979), (13, 10.933), (30, 1848.727), (31, 2500.000) };
		foreach (var (code, expected) in radius)
			Assert.Equal(expected, DcxEllipse.GetAxisLength(code), 0.0005);

		// B1 の C3/C4 は 1 つ小さいコードとの間を補間する。コード 0 の場合は 0 との間を補間する
		Assert.Equal(DcxEllipse.GetAxisLength(13), DcxEllipse.GetRefinedAxisLength(13, 0), 0.000001);
		Assert.Equal(DcxEllipse.GetAxisLength(13) - (DcxEllipse.GetAxisLength(13) - DcxEllipse.GetAxisLength(12)) / 2, DcxEllipse.GetRefinedAxisLength(13, 4), 0.000001);
		Assert.Equal(DcxEllipse.GetAxisLength(0) / 8, DcxEllipse.GetRefinedAxisLength(0, 7), 0.000001);
	}

	[Fact(DisplayName = "楕円の境界の点列が仕様書の内外判定の楕円上に乗る")]
	public void EllipsePolygon()
	{
		var ellipses = new DcxEllipse[]
		{
			// 4.2.3 の例 (新宿区付近)
			new(35.688, 139.691, 10.933, 5.979, 45),
			// 最大の楕円
			new(35, 135, 2500, 1848.727, -30),
			// 日付変更線付近
			new(-17.8, 178.9, 300, 100, 10),
		};
		foreach (var ellipse in ellipses)
		{
			var polygon = ellipse.ToPolygon(36);
			Assert.Equal(36, polygon.Length);
			foreach (var (latitude, longitude) in polygon)
				Assert.Equal(1, ellipse.EvaluateEllipseEquation(latitude, longitude), 0.000001);
			// 日付変更線をまたいでも経度は中心から連続する
			Assert.All(polygon, p => Assert.InRange(p.Longitude, ellipse.Longitude - 90, ellipse.Longitude + 90));

			Assert.True(ellipse.Contains(ellipse.Latitude, ellipse.Longitude));
			// 地球の裏側は楕円内に入らない
			Assert.False(ellipse.Contains(-ellipse.Latitude, ellipse.Longitude + 180));
		}

		// 方位角は東から北向きに測るため、45度の長軸は北東を向く
		var (northEastLatitude, northEastLongitude) = ellipses[0].ToPolygon(4)[0];
		Assert.True(northEastLatitude > ellipses[0].Latitude);
		Assert.True(northEastLongitude > ellipses[0].Longitude);
		Assert.False(ellipses[0].Contains(35.688 + 0.09, 139.691));
		Assert.True(ellipses[0].Contains(35.688 + 0.04, 139.691 + 0.04));
	}

	[Fact(DisplayName = "NULL メッセージや想定外の提供機関の電文は Vn を検証せず基本形として扱う")]
	public void NullAndUnknownProvider()
	{
		// NULL メッセージ (A2 のみ日本、その他は Vn も含めすべて 0)
		var nullMessage = ParseWithCorrectCrc(CreateBaseData(DCXReport.JapanCountryRegionCode, 0, 0));
		Assert.Equal(typeof(DCXReport), nullMessage.GetType());

		var unknownProvider = ParseWithCorrectCrc(CreateBaseData(DCXReport.JapanCountryRegionCode, 5, 0));
		Assert.Equal(typeof(DCXReport), unknownProvider.GetType());
	}

	[Fact(DisplayName = "国内向けの電文で非対応の Vn の場合は例外が出る")]
	public void InvalidVersion()
	{
		foreach (var provider in new byte[] { 1, 2, 3, 4 })
		{
			var ex = Record.Exception(() => ParseWithCorrectCrc(CreateBaseData(DCXReport.JapanCountryRegionCode, provider, 2)));
			Assert.IsType<DCReportParseException>(ex);
			Assert.StartsWith("この Vn には非対応です", ex.Message);
		}
	}

	[Fact(DisplayName = "国外機関からの電文は Vn によらず解析できる")]
	public void ParseOverseas()
	{
		// 10: Australia
		var data = CreateBaseData(10, 1, 5);
		TestUtils.SetValue(data, 146, 4, 0b1010);
		TestUtils.SetValue(data, 150, 64, 0x123456789ABCDEF);
		var report = Assert.IsType<OverseasDCXReport>(ParseWithCorrectCrc(data));

		Assert.Equal(10, report.CommonAlertMessage.CountryRegionCode);
		Assert.Equal(5, report.Version);
		Assert.Equal(((UInt128)0b1010 << 64) | 0x123456789ABCDEF, report.OrganizationSpecificArea);
	}

	[Fact(DisplayName = "自治体発信情報の追加対象地域を取得できる")]
	public void ParseLocalGovernment()
	{
		var data = CreateBaseData(DCXReport.JapanCountryRegionCode, 4, 1);
		TestUtils.SetValue(data, 146, 16, 13104); // 新宿区
		TestUtils.SetValue(data, 162, 1, 1);
		TestUtils.SetValue(data, 163, 17, 91522);
		TestUtils.SetValue(data, 180, 17, 68950);
		TestUtils.SetValue(data, 197, 5, 13);
		TestUtils.SetValue(data, 202, 5, 11);
		TestUtils.SetValue(data, 207, 7, 96);
		var report = Assert.IsType<LocalGovernmentDCXReport>(ParseWithCorrectCrc(data));

		Assert.Equal(13104, report.TargetAreaCode);
		var area = report.AdditionalTargetArea;
		Assert.NotNull(area);
		Assert.Equal(EvacuateDirectionType.Head, area.Direction);
		Assert.Equal(35.687, area.Ellipse.Latitude, 0.0005);
		Assert.Equal(139.689, area.Ellipse.Longitude, 0.0005);
		Assert.Equal(10.933, area.Ellipse.SemiMajorAxis, 0.0005);
		Assert.Equal(5.979, area.Ellipse.SemiMinorAxis, 0.0005);
		Assert.Equal(45, area.Ellipse.Azimuth, 0.000001);

		// EX2〜EX7 がすべて 0 の場合は指示なし
		var withoutArea = Assert.IsType<LocalGovernmentDCXReport>(ParseWithCorrectCrc(CreateBaseData(DCXReport.JapanCountryRegionCode, 4, 1)));
		Assert.Null(withoutArea.AdditionalTargetArea);
	}

	[Fact(DisplayName = "J アラートの市区町村コードのリストから 0 を除いて取得できる")]
	public void ParseJAlertMunicipalities()
	{
		var data = CreateBaseData(DCXReport.JapanCountryRegionCode, 3, 1);
		TestUtils.SetValue(data, 146, 1, 1);
		TestUtils.SetValue(data, 147, 16, 1101);
		TestUtils.SetValue(data, 163, 16, 13101);
		TestUtils.SetValue(data, 195, 16, 47201);
		var report = Assert.IsType<JAlertDCXReport>(ParseWithCorrectCrc(data));

		Assert.Equal(TargetAreaCodeListType.Municipality, report.TargetAreaCodeListType);
		Assert.Equal(new ushort[] { 1101, 13101, 47201 }, report.MunicipalityCodes);
		Assert.Empty(report.PrefectureCodes);
	}

	[Fact(DisplayName = "衛星指定と予約領域の違いは同じ内容とみなし、CAMF・拡張メッセージ・Vn の違いは区別する")]
	public void SameContent()
	{
		var baseData = Convert.FromHexString(LAlertChiyodaWithEllipse);
		var original = Assert.IsType<LAlertDCXReport>(DCReport.Parse(baseData));

		DCXReport Modify(int bitOffset, int bitCount)
		{
			var data = (byte[])baseData.Clone();
			for (var i = bitOffset; i < bitOffset + bitCount; i++)
				data[i / 8] ^= (byte)(1 << (7 - i % 8));
			// CRC (226〜249ビット目) を計算し直すため消去する
			data[28] &= 0xC0;
			data[29] = 0;
			data[30] = 0;
			data[31] &= 0x3F;
			return Assert.IsAssignableFrom<DCXReport>(ParseWithCorrectCrc(data));
		}

		// 衛星指定 (SD)
		Assert.True(original.HasSameContent(Modify(14, 10)));
		// 予約領域
		Assert.True(original.HasSameContent(Modify(220, 6)));
		// A1
		Assert.False(original.HasSameContent(Modify(24, 1)));
		// EX7 の最終ビット
		Assert.False(original.HasSameContent(Modify(213, 1)));
	}

	[Fact(DisplayName = "発生時刻を基準時刻の週から求め、週の境界をまたいだ場合は近い方の週にする")]
	public void HazardOnsetTime()
	{
		static CommonAlertMessage Create(bool nextWeek, ushort timeOfWeek)
			=> new(AlertMessageType.Alert, DCXReport.JapanCountryRegionCode, 1, 74, AlertSeverity.Severe, nextWeek, timeOfWeek, HazardDuration.Unknown, LibraryType.CountryRegion, 0, 0, 0, 0, 0, 0, 0, SpecificSettingsType.ImprovedResolutionOfMainEllipse, 0);

		// 2026/11/18 (水) 12:00 UTC
		var reference = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
		// 月曜 00:00
		Assert.Equal(new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero), Create(false, 1).GetHazardOnsetTime(reference));
		// 水曜 13:30
		Assert.Equal(new DateTimeOffset(2026, 11, 18, 13, 30, 0, TimeSpan.Zero), Create(false, 2 * 1440 + 13 * 60 + 30 + 1).GetHazardOnsetTime(reference));
		// 翌週の月曜 01:00
		Assert.Equal(new DateTimeOffset(2026, 11, 23, 1, 0, 0, TimeSpan.Zero), Create(true, 60 + 1).GetHazardOnsetTime(reference));
		// 日曜 23:59 に作られた電文を月曜 00:01 に受信した場合は前日とする
		Assert.Equal(new DateTimeOffset(2026, 11, 22, 23, 59, 0, TimeSpan.Zero), Create(false, CommonAlertMessage.MaxHazardOnsetTimeOfWeek).GetHazardOnsetTime(new DateTimeOffset(2026, 11, 23, 0, 1, 0, TimeSpan.Zero)));
		// 未使用・範囲外
		Assert.Null(Create(false, 0).GetHazardOnsetTime(reference));
		Assert.Null(Create(false, CommonAlertMessage.MaxHazardOnsetTimeOfWeek + 1).GetHazardOnsetTime(reference));
	}
}
