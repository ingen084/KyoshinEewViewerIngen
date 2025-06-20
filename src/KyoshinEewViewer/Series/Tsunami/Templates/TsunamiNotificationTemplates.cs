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
		case Level
			when "None"
				LevelString = "津波なし"
			when "Forecast"
				LevelString = "津波予報"
			when "Advisory"
				LevelString = "津波注意報"
			when "Warning"
				LevelString = "津波警報"
			when "MajorWarning"
				LevelString = "大津波警報"
			else
				LevelString = "津波情報"
		end
		case PreviousLevel
			when "None"
				PreviousLevelString = "津波なし"
			when "Forecast"
				PreviousLevelString = "津波予報"
			when "Advisory"
				PreviousLevelString = "津波注意報"
			when "Warning"
				PreviousLevelString = "津波警報"
			when "MajorWarning"
				PreviousLevelString = "大津波警報"
			else
				PreviousLevelString = "津波情報"
		end

		# 発表・解除・更新状態
		if PreviousLevel != Level
			if PreviousLevel == "None"
				LevelString + "が発表されました。"
			else if Level == "None"
				if PreviousLevel == "Forecast"
					"津波予報の期限が切れました。"
				else
					PreviousLevelString + "は解除されました。"
				end
			else
				PreviousLevelString + " は " + LevelString + " に切り替えられました。"
			end
		end}}

		{{# 対象地域情報
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
}