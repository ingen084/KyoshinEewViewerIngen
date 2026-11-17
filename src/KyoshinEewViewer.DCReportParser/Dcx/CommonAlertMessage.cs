namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// 共通警報メッセージフォーマット (CAMF, Common Alert Message Format)
/// </summary>
/// <param name="MessageType">A1 メッセージ種別</param>
/// <param name="CountryRegionCode">A2 国・地域名コード</param>
/// <param name="ProviderIdentifier">A3 提供機関の識別子 (意味は国・地域によって異なる)</param>
/// <param name="HazardCategoryAndType">A4 災害のカテゴリと種別</param>
/// <param name="Severity">A5 深刻度</param>
/// <param name="IsHazardOnsetNextWeek">A6 発生が翌週かどうか</param>
/// <param name="HazardOnsetTimeOfWeek">A7 週内の発生時刻 (月曜 00:00 UTC を 1 とする分単位。0 は未使用)</param>
/// <param name="HazardDuration">A8 継続時間</param>
/// <param name="LibraryType">A9 ライブラリの種類</param>
/// <param name="LibraryVersion">A10 ライブラリのバージョン (0 が #1)</param>
/// <param name="InstructionCode">A11 指示ライブラリのコード (10ビット)</param>
/// <param name="EllipseCentreLatitudeCode">A12 楕円中心の緯度コード</param>
/// <param name="EllipseCentreLongitudeCode">A13 楕円中心の経度コード</param>
/// <param name="EllipseSemiMajorAxisCode">A14 楕円の長半径コード</param>
/// <param name="EllipseSemiMinorAxisCode">A15 楕円の短半径コード</param>
/// <param name="EllipseAzimuthCode">A16 楕円の方位角コード</param>
/// <param name="SpecificSettingsType">A17 追加設定の種類</param>
/// <param name="SpecificSettings">A18 追加設定 (15ビット)</param>
public record CommonAlertMessage(
	AlertMessageType MessageType,
	ushort CountryRegionCode,
	byte ProviderIdentifier,
	byte HazardCategoryAndType,
	AlertSeverity Severity,
	bool IsHazardOnsetNextWeek,
	ushort HazardOnsetTimeOfWeek,
	HazardDuration HazardDuration,
	LibraryType LibraryType,
	byte LibraryVersion,
	ushort InstructionCode,
	ushort EllipseCentreLatitudeCode,
	uint EllipseCentreLongitudeCode,
	byte EllipseSemiMajorAxisCode,
	byte EllipseSemiMinorAxisCode,
	byte EllipseAzimuthCode,
	SpecificSettingsType SpecificSettingsType,
	ushort SpecificSettings
)
{
	/// <summary>
	/// A7 が取りうる最大値 (日曜 23:59 UTC)
	/// </summary>
	public const ushort MaxHazardOnsetTimeOfWeek = 7 * 24 * 60;

	/// <summary>
	/// 対象地域を示す楕円 (A12〜A16)<br/>
	/// A17 が B1 の場合は A18 による補正を適用する。A12〜A16 がすべて 0 の場合は楕円を使用しないため null
	/// </summary>
	public DcxEllipse? Ellipse
	{
		get {
			if (EllipseCentreLatitudeCode == 0 && EllipseCentreLongitudeCode == 0 && EllipseSemiMajorAxisCode == 0 && EllipseSemiMinorAxisCode == 0 && EllipseAzimuthCode == 0)
				return null;

			var latitude = DcxEllipse.GetMainLatitude(EllipseCentreLatitudeCode);
			var longitude = DcxEllipse.GetMainLongitude(EllipseCentreLongitudeCode);
			var semiMajorAxis = DcxEllipse.GetAxisLength(EllipseSemiMajorAxisCode);
			var semiMinorAxis = DcxEllipse.GetAxisLength(EllipseSemiMinorAxisCode);

			// B1: 各 3 ビットで中心位置と半径の分解能を上げる (先頭から C1, C2, C3, C4)
			if (SpecificSettingsType == SpecificSettingsType.ImprovedResolutionOfMainEllipse)
			{
				latitude += ((SpecificSettings >> 12) & 0b111) * DcxEllipse.MainLatitudeInterval / 8;
				longitude += ((SpecificSettings >> 9) & 0b111) * DcxEllipse.MainLongitudeInterval / 8;
				semiMajorAxis = DcxEllipse.GetRefinedAxisLength(EllipseSemiMajorAxisCode, (SpecificSettings >> 6) & 0b111);
				semiMinorAxis = DcxEllipse.GetRefinedAxisLength(EllipseSemiMinorAxisCode, (SpecificSettings >> 3) & 0b111);
			}

			return new DcxEllipse(latitude, longitude, semiMajorAxis, semiMinorAxis, DcxEllipse.GetMainAzimuth(EllipseAzimuthCode));
		}
	}

	/// <summary>
	/// 継続時間 (A8) の上限。不明の場合は null
	/// </summary>
	public TimeSpan? HazardDurationUpperBound => HazardDuration switch
	{
		HazardDuration.LessThan6Hours => TimeSpan.FromHours(6),
		HazardDuration.From6To12Hours => TimeSpan.FromHours(12),
		HazardDuration.From12To24Hours => TimeSpan.FromHours(24),
		_ => null,
	};

	/// <summary>
	/// A6/A7 から発生日時を求める
	/// </summary>
	/// <param name="reference">基準とする時刻 (受信時刻など)</param>
	/// <returns>発生日時 (UTC)。A7 が未使用または範囲外の場合は null</returns>
	public DateTimeOffset? GetHazardOnsetTime(DateTimeOffset reference)
	{
		if (HazardOnsetTimeOfWeek is 0 or > MaxHazardOnsetTimeOfWeek)
			return null;

		var referenceUtc = reference.UtcDateTime;
		// 週は月曜 00:00 (UTC) に始まる
		var weekStart = new DateTimeOffset(referenceUtc.Date.AddDays(-(((int)referenceUtc.DayOfWeek + 6) % 7)), TimeSpan.Zero);

		// 週の境界付近で受信した場合、電文を作成した時点の週が隣の週である可能性があるため、
		// 隣の週も候補に含めて基準時刻に最も近いものを採用する
		var margin = TimeSpan.FromDays(1);
		var candidates = new List<DateTimeOffset> { weekStart };
		if (reference - weekStart < margin)
			candidates.Add(weekStart.AddDays(-7));
		if (weekStart.AddDays(7) - reference < margin)
			candidates.Add(weekStart.AddDays(7));

		return candidates
			.Select(s => s.AddDays(IsHazardOnsetNextWeek ? 7 : 0).AddMinutes(HazardOnsetTimeOfWeek - 1))
			.MinBy(t => (t - reference).Duration());
	}
}

/// <summary>
/// A1 メッセージ種別
/// </summary>
public enum AlertMessageType
{
	/// <summary>
	/// 試験
	/// </summary>
	Test = 0b00,
	/// <summary>
	/// 発表
	/// </summary>
	Alert = 0b01,
	/// <summary>
	/// 更新
	/// </summary>
	Update = 0b10,
	/// <summary>
	/// 解除
	/// </summary>
	AllClear = 0b11,
}

/// <summary>
/// A5 深刻度
/// </summary>
public enum AlertSeverity
{
	/// <summary>
	/// 不明
	/// </summary>
	Unknown = 0b00,
	/// <summary>
	/// 生命・財産への脅威の可能性
	/// </summary>
	Moderate = 0b01,
	/// <summary>
	/// 生命・財産への重大な脅威
	/// </summary>
	Severe = 0b10,
	/// <summary>
	/// 生命・財産への極めて重大な脅威
	/// </summary>
	Extreme = 0b11,
}

/// <summary>
/// A8 継続時間
/// </summary>
public enum HazardDuration
{
	/// <summary>
	/// 不明
	/// </summary>
	Unknown = 0b00,
	/// <summary>
	/// 6時間未満
	/// </summary>
	LessThan6Hours = 0b01,
	/// <summary>
	/// 6時間以上12時間未満
	/// </summary>
	From6To12Hours = 0b10,
	/// <summary>
	/// 12時間以上24時間未満
	/// </summary>
	From12To24Hours = 0b11,
}

/// <summary>
/// A9 ライブラリの種類
/// </summary>
public enum LibraryType
{
	/// <summary>
	/// 国際ライブラリ
	/// </summary>
	International = 0,
	/// <summary>
	/// 国・地域ライブラリ
	/// </summary>
	CountryRegion = 1,
}

/// <summary>
/// A17 追加設定の種類
/// </summary>
public enum SpecificSettingsType
{
	/// <summary>
	/// B1 楕円の分解能の改善
	/// </summary>
	ImprovedResolutionOfMainEllipse = 0b00,
	/// <summary>
	/// B2 災害の中心位置
	/// </summary>
	PositionOfCentreOfHazard = 0b01,
	/// <summary>
	/// B3 第2楕円の定義
	/// </summary>
	SecondaryEllipseDefinition = 0b10,
	/// <summary>
	/// B4 災害の定量的・詳細な情報
	/// </summary>
	QuantitativeAndDetailedInformation = 0b11,
}
