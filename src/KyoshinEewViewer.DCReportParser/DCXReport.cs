using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser;

/// <summary>
/// 災危通報(拡張) DCX メッセージ (MT44)<br/>
/// このクラスそのものは NULL メッセージや想定外の提供機関の電文を表す。処理対象の電文は派生クラスになる
/// </summary>
public class DCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignation satelliteDesignation, CommonAlertMessage commonAlertMessage, byte version) : DCReport(rawData, preamble, messageType)
{
	/// <summary>
	/// 日本を示す国・地域名コード (A2)
	/// </summary>
	public const ushort JapanCountryRegionCode = 111;

	// 各フィールドの開始位置 (ビット)
	private const int CamfOffset = 24;
	private const int ExtendedMessageOffset = 146;
	private const int VersionOffset = 214;
	// 重複判定に使用する範囲 (CAMF から拡張メッセージの終わりまで)
	private const int ContentEndOffset = 220;

	public static DCXReport Parse(byte[] data, Preamble pab, byte mt)
	{
		var sd = new SatelliteDesignation((SatelliteDesignationMaskType)GetValue(data, 14, 1), (ushort)GetValue(data, 15, 9));
		var camf = ParseCommonAlertMessage(data);
		var vn = (byte)GetValue(data, VersionOffset, 6);

		// 国外機関の情報は Vn も機関ごとの管理となる
		if (camf.CountryRegionCode != JapanCountryRegionCode)
			return new OverseasDCXReport(data, pab, mt, sd, camf, vn, ((UInt128)(ulong)GetValue(data, ExtendedMessageOffset, 4) << 64) | (ulong)GetValue(data, ExtendedMessageOffset + 4, 64));

		// NULL メッセージは提供機関が 0 で Vn も 0 のため、Vn の検証より先に種別を判定する
		if (camf.ProviderIdentifier is not (1 or 2 or 3 or 4))
			return new DCXReport(data, pab, mt, sd, camf, vn);

		// 仕様書では 1 固定だが、実際の配信では 0 の電文 (関係省庁の J アラートなど) もあるため受け付ける
		if (vn is not (0 or 1))
			throw new DCReportParseException("この Vn には非対応です: " + vn);

		return camf.ProviderIdentifier switch
		{
			1 => new LAlertDCXReport(data, pab, mt, sd, camf, vn, (ushort)GetValue(data, ExtendedMessageOffset, 16)),
			4 => new LocalGovernmentDCXReport(data, pab, mt, sd, camf, vn, (ushort)GetValue(data, ExtendedMessageOffset, 16), ParseAdditionalTargetArea(data)),
			_ => ParseJAlert(data, pab, mt, sd, camf, vn),
		};
	}

	private static CommonAlertMessage ParseCommonAlertMessage(byte[] data)
	{
		var offset = CamfOffset;
		long Next(int bitCount)
		{
			var value = GetValue(data, offset, bitCount);
			offset += bitCount;
			return value;
		}

		return new CommonAlertMessage(
			MessageType: (AlertMessageType)Next(2),
			CountryRegionCode: (ushort)Next(9),
			ProviderIdentifier: (byte)Next(5),
			HazardCategoryAndType: (byte)Next(7),
			Severity: (AlertSeverity)Next(2),
			IsHazardOnsetNextWeek: Next(1) == 1,
			HazardOnsetTimeOfWeek: (ushort)Next(14),
			HazardDuration: (HazardDuration)Next(2),
			LibraryType: (LibraryType)Next(1),
			LibraryVersion: (byte)Next(3),
			InstructionCode: (ushort)Next(10),
			EllipseCentreLatitudeCode: (ushort)Next(16),
			EllipseCentreLongitudeCode: (uint)Next(17),
			EllipseSemiMajorAxisCode: (byte)Next(5),
			EllipseSemiMinorAxisCode: (byte)Next(5),
			EllipseAzimuthCode: (byte)Next(6),
			SpecificSettingsType: (SpecificSettingsType)Next(2),
			SpecificSettings: (ushort)Next(15)
		);
	}

	private static AdditionalTargetArea? ParseAdditionalTargetArea(byte[] data)
	{
		var direction = (EvacuateDirectionType)GetValue(data, 162, 1);
		var latitude = GetValue(data, 163, 17);
		var longitude = GetValue(data, 180, 17);
		var semiMajorAxis = (int)GetValue(data, 197, 5);
		var semiMinorAxis = (int)GetValue(data, 202, 5);
		var azimuth = (int)GetValue(data, 207, 7);

		// EX2〜EX7 がすべて 0 の場合は指示なし
		if (direction == 0 && latitude == 0 && longitude == 0 && semiMajorAxis == 0 && semiMinorAxis == 0 && azimuth == 0)
			return null;

		return new AdditionalTargetArea(direction, new DcxEllipse(
			DcxEllipse.GetAdditionalLatitude(latitude),
			DcxEllipse.GetAdditionalLongitude(longitude),
			DcxEllipse.GetAxisLength(semiMajorAxis),
			DcxEllipse.GetAxisLength(semiMinorAxis),
			DcxEllipse.GetAdditionalAzimuth(azimuth)
		));
	}

	private static JAlertDCXReport ParseJAlert(byte[] data, Preamble pab, byte mt, SatelliteDesignation sd, CommonAlertMessage camf, byte vn)
	{
		var listType = (TargetAreaCodeListType)GetValue(data, ExtendedMessageOffset, 1);
		if (listType == TargetAreaCodeListType.Prefecture)
			return new JAlertDCXReport(data, pab, mt, sd, camf, vn, listType, (ulong)GetValue(data, 147, 47), []);

		var municipalityCodes = Enumerable.Range(0, 4)
			.Select(i => (ushort)GetValue(data, 147 + i * 16, 16))
			.Where(c => c != 0)
			.ToArray();
		return new JAlertDCXReport(data, pab, mt, sd, camf, vn, listType, 0, municipalityCodes);
	}

	/// <summary>
	/// 衛星指定 (SD)
	/// </summary>
	public SatelliteDesignation SatelliteDesignation { get; } = satelliteDesignation;

	/// <summary>
	/// 共通警報メッセージフォーマット (CAMF)
	/// </summary>
	public CommonAlertMessage CommonAlertMessage { get; } = commonAlertMessage;

	/// <summary>
	/// 電文のバージョン (Vn)
	/// </summary>
	public byte Version { get; } = version;

	/// <summary>
	/// 試験の電文かどうか
	/// </summary>
	public bool IsTest => CommonAlertMessage.MessageType == AlertMessageType.Test;

	/// <summary>
	/// 内容 (CAMF と拡張メッセージ) が同じ電文かどうかを判定する<br/>
	/// 衛星指定は同じ内容でも送信ごとに変わりうるため比較しない
	/// </summary>
	public bool HasSameContent(DCXReport other)
	{
		for (var offset = CamfOffset; offset < ContentEndOffset; offset += 49)
		{
			var bitCount = Math.Min(49, ContentEndOffset - offset);
			if (GetValue(offset, bitCount) != other.GetValue(offset, bitCount))
				return false;
		}
		return true;
	}
}
