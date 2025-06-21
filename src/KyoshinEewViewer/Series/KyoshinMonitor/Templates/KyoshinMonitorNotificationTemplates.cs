namespace KyoshinEewViewer.Series.KyoshinMonitor.Templates;

/// <summary>
/// 強震モニタ関連のScribanテンプレート定数
/// </summary>
public static class KyoshinMonitorNotificationTemplates
{
	/// <summary>
	/// EEW通知用テンプレート
	/// </summary>
	public const string EewNotificationMessage = """
		{{IntensityLongName
		if IsIntensityOver; "以上"; end

		if EpicenterPlaceName
			$"/{EpicenterPlaceName}"
		end

		if Magnitude
			$"/M{Magnitude | math.format "F1"}"
		end

		if Depth && !IsTemporaryEpicenter
			$"/{Depth}km"
		end

		if IsWarning && WarningAreaNames
			$" 【{WarningAreaNames | array.join "・"}】"
		end}}
		""";


	/// <summary>
	/// 揺れ検知通知用テンプレート
	/// </summary>
	public const string ShakeDetectedNotificationMessage = """
		{{if IsReplay; "[リプレイ] "; end
		"揺れ検知 "
		if Level == 1
			"弱い"
		else if Level == 2
			"普通"
		else if Level == 3
			"強い"
		else if Level == 4
			"非常に強い"
		else
			"不明"
		end
		if Regions && Regions.size > 0
			$" {Regions | array.join "・"}地方"
		end}}
		""";


	/// <summary>
	/// EEW通知タイトル用テンプレート
	/// </summary>
	public const string EewNotificationTitle = """
		{{if IsCancelled; "[取消] "; end
		if IsReplay; "[リプレイ] "; end
		"緊急地震速報"
		if IsFinal; "(最終"; else; "("; end
		SerialNo | math.format "D2"
		"報)"}}
		""";

	/// <summary>
	/// 揺れ検知通知タイトル用テンプレート
	/// </summary>
	public const string ShakeDetectedNotificationTitle = """
		{{if IsReplay; "[リプレイ] "; end
		"揺れ検知"}}
		""";

	/// <summary>
	/// EEW音声読み上げ用分離テンプレート
	/// </summary>
	public static readonly string[] EewVoiceNotificationParts = [
		"{{if IsReplay}}リプレイ中です{{end}}",
		"{{if IsCancelled}}緊急地震速報は取り消されました{{else if EventSubType == \"Final\"}}緊急地震速報最終報{{else}}緊急地震速報{{end}}",
		"{{if !IsCancelled && EventSubType != \"Cancel\" && EventSubType != \"CancelWarning\"}}最大{{IntensityLongName}}{{if IsIntensityOver}}以上{{end}}{{end}}",
		"{{if !IsCancelled && EventSubType != \"Cancel\" && EventSubType != \"CancelWarning\" && EpicenterPlaceName}}{{EpicenterPlaceName}}{{end}}",
	];

	/// <summary>
	/// 揺れ検知音声読み上げ用分離テンプレート
	/// </summary>
	public static readonly string[] ShakeDetectedVoiceNotificationParts = [
		"{{if IsReplay}}リプレイ中です{{end}}",
		"""
		{{case Level
			when "Weak"
				"弱い"
			when "Strong"
				"強い"
			when "Stronger"
				"非常に強い"
		end}}揺れを検知しました
		"""
	];

	/// <summary>
	/// EEW音声読み上げ用テンプレート
	/// </summary>
	public static readonly string[] EewVoiceNotification = EewVoiceNotificationParts;

	/// <summary>
	/// 揺れ検知音声読み上げ用テンプレート
	/// </summary>
	public static readonly string[] ShakeDetectedVoiceNotification = ShakeDetectedVoiceNotificationParts;
}