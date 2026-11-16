using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Dcx;
using KyoshinEewViewer.Series.Qzss.Models;

namespace KyoshinEewViewer.Tests.Qzss;

public class DCXReportGroupTests
{
	private const byte FMMC = 1;
	private const byte FDMA = 2;
	private const byte Rainfall = 74;
	private const byte Flood = 68;
	private const byte MissileAttack = 8;

	// 2026/11/18 (水) 12:00 UTC
	private static readonly DateTimeOffset BaseTime = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

	private static DCReport LAlert(AlertMessageType type, ushort area, AlertSeverity severity = AlertSeverity.Severe, byte hazard = Rainfall, ushort instruction = 0)
		=> new DCXTestMessage(FMMC, type, hazard, severity).TargetArea(area).Instruction(instruction).Parse();

	[Fact(DisplayName = "L アラートは同じ災害の電文を市区町村ごとにまとめ、更新・解除を該当する市区町村に反映する")]
	public void LAlertMergesAreas()
	{
		var group = new DCXAreaReportGroup((DCXReport)LAlert(AlertMessageType.Alert, 13101, AlertSeverity.Moderate), BaseTime, null);
		Assert.Equal("Lアラート", group.KindName);
		Assert.Equal("大雨", group.HazardName);
		Assert.Equal("マルチメディア振興センター", group.ProviderName);
		Assert.False(group.IsTestOrDrill);

		// 別の市区町村
		Assert.True(group.TryProcess(LAlert(AlertMessageType.Alert, 13104, AlertSeverity.Moderate), BaseTime.AddMinutes(1)));
		// 同じ市区町村の更新 (A11: 向かえ。3階以上。)
		Assert.True(group.TryProcess(LAlert(AlertMessageType.Update, 13101, AlertSeverity.Extreme, instruction: 0b10_00000010), BaseTime.AddMinutes(2)));

		Assert.Equal(3, group.ReportCount);
		Assert.Equal([13101, 13104], group.Areas.Select(a => (int)a.AreaCode));
		Assert.Equal("東京都千代田区", group.Areas[0].AreaName);
		Assert.Equal("更新", group.Areas[0].MessageTypeName);
		Assert.Equal("向かえ。3階以上。", group.Areas[0].InstructionText);
		Assert.Equal(AlertSeverity.Extreme, group.MaxSeverity);
		Assert.Equal(2, group.AreaCount);
		Assert.Equal("発表中", group.StatusName);

		// 別の災害・試験の電文はまとめない
		Assert.False(group.TryProcess(LAlert(AlertMessageType.Alert, 13101, hazard: Flood), BaseTime.AddMinutes(3)));
		Assert.False(group.TryProcess(LAlert(AlertMessageType.Test, 13101), BaseTime.AddMinutes(3)));

		// すべての市区町村が解除されたら、次の発表は新しいグループにする
		Assert.True(group.TryProcess(LAlert(AlertMessageType.AllClear, 13101), BaseTime.AddMinutes(4)));
		Assert.False(group.IsAllCleared);
		Assert.Equal(AlertSeverity.Moderate, group.MaxSeverity);
		Assert.True(group.TryProcess(LAlert(AlertMessageType.AllClear, 13104), BaseTime.AddMinutes(5)));
		Assert.True(group.IsAllCleared);
		Assert.Equal("解除", group.StatusName);
		Assert.False(group.TryProcess(LAlert(AlertMessageType.Alert, 13101), BaseTime.AddMinutes(6)));
	}

	[Fact(DisplayName = "L アラートは有効期限 (発生時刻と継続時間、不明なら受信から1週間) を過ぎたら別のグループにする")]
	public void LAlertExpires()
	{
		// 水曜 12:00 発生、6時間未満
		var onset = (ushort)(2 * 1440 + 12 * 60 + 1);
		var group = new DCXAreaReportGroup((DCXReport)new DCXTestMessage(FMMC, AlertMessageType.Alert, Rainfall).TargetArea(13101).HazardOnset(onset, HazardDuration.LessThan6Hours).Parse(), BaseTime, null);
		Assert.Equal(BaseTime.AddHours(6), group.Areas[0].ExpiresAt);
		Assert.True(group.TryProcess(LAlert(AlertMessageType.Alert, 13104), BaseTime.AddHours(5)));
		// 13104 は継続時間が不明なので受信から 1 週間有効
		Assert.True(group.TryProcess(LAlert(AlertMessageType.Update, 13105), BaseTime.AddDays(6)));
		// すべての市区町村が有効期限を過ぎた後はまとめない
		Assert.False(group.TryProcess(LAlert(AlertMessageType.Update, 13105, AlertSeverity.Extreme), BaseTime.AddDays(14)));
	}

	[Fact(DisplayName = "同じ内容の電文は重複として扱うが、日付を持たないため受信から1日以上経過したものは重複としない")]
	public void Duplicate()
	{
		var report = (DCXReport)LAlert(AlertMessageType.Alert, 13101);
		var group = new DCXAreaReportGroup(report, BaseTime, null);

		Assert.True(group.CheckDuplicate(LAlert(AlertMessageType.Alert, 13101), BaseTime.AddMinutes(1)));
		Assert.False(group.CheckDuplicate(LAlert(AlertMessageType.Alert, 13104), BaseTime.AddMinutes(1)));
		// 定期的な試験用データの配信などで、同じ内容の電文が翌週に再び送られる
		Assert.False(group.CheckDuplicate(LAlert(AlertMessageType.Alert, 13101), BaseTime.AddDays(7)));

		// 統合した電文も受信時刻を基準にする
		Assert.True(group.TryProcess(LAlert(AlertMessageType.Update, 13101), BaseTime.AddDays(2)));
		Assert.True(group.CheckDuplicate(LAlert(AlertMessageType.Update, 13101), BaseTime.AddDays(2.5)));
		Assert.False(group.CheckDuplicate(LAlert(AlertMessageType.Alert, 13101), BaseTime.AddDays(2.5)));
	}

	[Fact(DisplayName = "J アラートは短時間に続けて受信した電文をまとめ、指示の推移を新しい順に保持する")]
	public void JAlertMergesHistory()
	{
		var launched = (JAlertDCXReport)new DCXTestMessage(FDMA, AlertMessageType.Alert, MissileAttack, AlertSeverity.Extreme).Instruction(0x80).Prefectures(1, 2, 47).Parse();
		var group = new JAlertReportGroup(launched, BaseTime, null);
		Assert.Equal("Jアラート", group.KindName);
		Assert.Equal("消防庁", group.ProviderName);
		Assert.Equal("ミサイル発射", group.Summary);
		Assert.Equal(["北海道", "青森県", "沖縄県"], group.TargetAreaNames);
		Assert.Equal(3, group.TargetAreaCount);

		var passed = new DCXTestMessage(FDMA, AlertMessageType.Alert, MissileAttack, AlertSeverity.Extreme).Instruction(0x81).Prefectures(Enumerable.Range(1, 47).ToArray()).Parse();
		Assert.True(group.TryProcess(passed, BaseTime.AddMinutes(10)));
		Assert.Equal("ミサイル通過", group.Summary);
		Assert.Equal(["全都道府県"], group.TargetAreaNames);
		Assert.Equal(47, group.TargetAreaCount);
		Assert.Equal(["ミサイル通過", "ミサイル発射"], group.History.Select(h => h.Summary));

		// 前回の受信から 1 時間を超えた電文は別の事案とする
		var next = new DCXTestMessage(FDMA, AlertMessageType.Alert, MissileAttack, AlertSeverity.Extreme).Instruction(0x84).Prefectures(47).Parse();
		Assert.False(group.TryProcess(next, BaseTime.AddMinutes(71)));
	}

	[Fact(DisplayName = "J アラートの解除は誤報による取消として扱い、誤報の指示文や対象地域の着色を表示しない")]
	public void JAlertFalseAlert()
	{
		var launched = (JAlertDCXReport)new DCXTestMessage(FDMA, AlertMessageType.Alert, MissileAttack, AlertSeverity.Extreme).Instruction(0x80).Prefectures(47).Parse();
		var group = new JAlertReportGroup(launched, BaseTime, null);
		Assert.NotNull(group.MapDisplayParameter.CustomColorMap);

		var falseAlert = new DCXTestMessage(FDMA, AlertMessageType.AllClear, MissileAttack, AlertSeverity.Extreme).Instruction(0x80).Prefectures(47).Parse();
		Assert.True(group.TryProcess(falseAlert, BaseTime.AddMinutes(3)));
		Assert.Equal("取消", group.MessageTypeName);
		Assert.Equal("誤報による取消", group.Summary);
		Assert.Null(group.InstructionText);
		Assert.Null(group.MapDisplayParameter.CustomColorMap);
	}

	[Fact(DisplayName = "対象地域は楕円がある場合は楕円で示し、楕円がない場合のみ市区町村を着色する")]
	public void TargetAreaColoring()
	{
		var withoutEllipse = new DCXAreaReportGroup((DCXReport)LAlert(AlertMessageType.Alert, 13101), BaseTime, null);
		var customColorMap = withoutEllipse.MapDisplayParameter.CustomColorMap;
		Assert.NotNull(customColorMap);
		Assert.Equal([1310100L], Assert.Single(customColorMap).Value.Keys);

		// 自治体発信情報: 楕円 (仕様書 4.2.3 の例) で新宿区の一部を示す
		var withEllipse = new DCXAreaReportGroup((DCXReport)new DCXTestMessage(4, AlertMessageType.Alert, Rainfall).TargetArea(13104)
			.Set(80, 16, 45761).Set(96, 17, 116395).Set(113, 5, 13).Set(118, 5, 11).Set(123, 6, 48).Parse(), BaseTime, null);
		Assert.NotNull(withEllipse.Areas[0].Ellipse);
		Assert.Null(withEllipse.MapDisplayParameter.CustomColorMap);
	}
}
