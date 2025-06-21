namespace KyoshinEewViewer.Series.Earthquake.Templates;

/// <summary>
/// 地震情報関連のScribanテンプレート定数
/// </summary>
public static class EarthquakeNotificationTemplates
{
	/// <summary>
	/// 通知用テンプレート
	/// </summary>
	public const string NotificationMessage = """
		{{
		# 状態表示
		if IsCancelled; "[取消] "; end
		if IsTrainingOrTest; "[訓練/試験] "; end
		if IsTest; "[テスト] "; end

		# 基本震度情報
		$"最大{MaxIntensityLongName}"

		# 震源情報（震源のみ報または詳細震度適用時のみ）
		if Hypocenter && (IsHypocenterOnly || IsDetailIntensityApplied)
			$"/{Hypocenter.OccurrenceAt | date.to_string "%H:%M"}"
			$"/{Hypocenter.PlaceName}"
			
			# 深さ情報
			if !Hypocenter.IsNoDepthData
				if Hypocenter.IsVeryShallow
					"/ごく浅い"
				else
					$"/{Hypocenter.Depth}km"
				end
			end
			
			# マグニチュード情報
			if Hypocenter.MagnitudeAlternativeText
				$"/{Hypocenter.MagnitudeAlternativeText}"
			else
				$"/M{Hypocenter.Magnitude | math.format "F1"}"
			end
		end}}
		""";

	/// <summary>
	/// 地震情報音声読み上げ用テンプレート
	/// </summary>
	public static readonly string[] VoiceNotificationParts = [
		"{{if IsTrainingOrTest}}これは訓練もしくは試験です{{end}}",
		"{{if IsCancelled}}地震情報は取り消されました{{else}}{{if IsTest}}ワークフローのテストです{{end}}{{end}}",
		"{{if !IsCancelled && Hypocenter && Hypocenter.PlaceName}}{{Hypocenter.PlaceName}}で{{end}}",
		"{{if !IsCancelled}}最大{{MaxIntensityLongName}}の地震が発生しました{{end}}",
		"{{if !IsCancelled && Hypocenter && !Hypocenter.IsNoDepthData}}{{if Hypocenter.IsVeryShallow}}深さはごく浅い{{else}}深さ{{Hypocenter.Depth}}キロ{{end}}{{end}}",
		"{{if !IsCancelled && Hypocenter}}マグニチュードは{{Hypocenter.Magnitude | math.format \"F1\"}}です{{end}}",
		"{{if !IsCancelled && Comment}}{{Comment}}{{end}}",
	];

	/// <summary>
	/// 地震情報通知タイトル用テンプレート
	/// </summary>
	public const string NotificationTitle = """
		{{if IsCancelled; "[取消] "; end
		if IsTrainingOrTest; "[訓練/試験] "; end
		if IsTest; "[テスト] "; end
		LatestInformationName}}
		""";
}
