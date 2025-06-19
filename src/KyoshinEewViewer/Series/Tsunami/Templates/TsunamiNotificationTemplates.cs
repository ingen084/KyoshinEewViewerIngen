namespace KyoshinEewViewer.Series.Tsunami.Templates;

/// <summary>
/// 津波情報関連のScribanテンプレート定数
/// </summary>
public static class TsunamiNotificationTemplates
{
	/// <summary>
	/// 通知用テンプレート
	/// </summary>
	public const string NotificationMessage = """
		{{
		# 状態表示
		if TsunamiInfo && TsunamiInfo.SpecialState; "[" + TsunamiInfo.SpecialState + "] "; end

		# 基本津波情報
		if Level == "None"
			"津波なし"
		else if Level == "Forecast"
			"津波予報"
		else if Level == "Advisory"
			"津波注意報"
		else if Level == "Warning"
			"津波警報"
		else if Level == "MajorWarning"
			"大津波警報"
		else
			"津波情報"
		end

		# 発表・解除・更新状態
		if PreviousLevel != Level
			if PreviousLevel == "None"
				"発表"
			else if Level == "None"
				"解除"
			else
				"更新"
			end
		end

		# 対象地域情報
		if TsunamiInfo && Level != "None"
			if TsunamiInfo.MajorWarningAreas
				$" 【{TsunamiInfo.MajorWarningAreas | array.map "Name" | array.join "・"}】"
			else if TsunamiInfo.WarningAreas
				$" 【{TsunamiInfo.WarningAreas | array.map "Name" | array.join "・"}】"
			else if TsunamiInfo.AdvisoryAreas
				$" ({TsunamiInfo.AdvisoryAreas | array.map "Name" | array.join "・"})"
			end
		end}}
		""";


	/// <summary>
	/// 津波情報通知タイトル用テンプレート
	/// </summary>
	public const string NotificationTitle = """
		{{if TsunamiInfo && TsunamiInfo.SpecialState; "[" + TsunamiInfo.SpecialState + "] "; end
		if Level == "None"
			"津波解除"
		else if Level == "Forecast"
			"津波予報"
		else if Level == "Advisory"
			"津波注意報"
		else if Level == "Warning"
			"津波警報"
		else if Level == "MajorWarning"
			"大津波警報"
		else
			"津波情報"
		end}}
		""";

	/// <summary>
	/// 津波情報音声読み上げ用分離テンプレート
	/// </summary>
	public static readonly string[] VoiceNotificationParts = [
		"{{if TsunamiInfo && TsunamiInfo.SpecialState}}{{TsunamiInfo.SpecialState}}です{{end}}",
		"""
		{{if Level == "None"}}{{if PreviousLevel != "None"}}津波警報等はすべて解除されました{{else}}津波の心配はありません{{end}}{{else if Level == "Forecast"}}津波予報が発表されました{{else if Level == "Advisory"}}津波注意報が発表されました{{else if Level == "Warning"}}津波警報が発表されました{{else if Level == "MajorWarning"}}大津波警報が発表されました{{end}}
		""",
		"""
		{{if TsunamiInfo && Level != "None"}}{{if TsunamiInfo.MajorWarningAreas}}大津波警報の対象地域は{{TsunamiInfo.MajorWarningAreas | array.map "Name" | array.join "、"}}です{{else if TsunamiInfo.WarningAreas}}津波警報の対象地域は{{TsunamiInfo.WarningAreas | array.map "Name" | array.join "、"}}です{{else if TsunamiInfo.AdvisoryAreas}}津波注意報の対象地域は{{TsunamiInfo.AdvisoryAreas | array.map "Name" | array.join "、"}}です{{end}}{{end}}
		"""
	];

	/// <summary>
	/// 音声読み上げ用テンプレート
	/// </summary>
	public static readonly string[] VoiceNotification = VoiceNotificationParts;
}