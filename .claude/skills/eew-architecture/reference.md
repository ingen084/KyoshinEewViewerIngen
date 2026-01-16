# EEW Reference

## EewWarningAreas Model

警報地域情報を保持するモデル（`Eew.cs`内で定義）：

```csharp
public record EewWarningAreas
{
    public required string DisplaySource { get; init; }  // 警報地域のソース表示名
    public required int SerialNo { get; init; }          // 報数
    public required int[] Codes { get; init; }           // 警報地域コード配列
    public required string[] Names { get; init; }        // 警報地域名配列
    public bool IsWarningTelegram { get; init; }         // 警報電文由来かどうか
}
```

## MixEew Logic

`EewController.MixEew()`の詳細な優先順位：

| 現在のソース | 受信したソース | EewUpdateReason | 動作 |
|------------|--------------|-----------------|------|
| KyoshinMonitor | Dmdata | MorePriority | 受信データで更新 |
| SignalNowProfessional | Dmdata | MorePriority | 受信データで更新 |
| Axis | Dmdata | MorePriority | 受信データで更新 |
| KyoshinMonitor | SignalNowProfessional | AccuracySupport | 精度情報のみ補完 |
| Axis | SignalNowProfessional | AccuracySupport | 精度情報のみ補完 |
| SignalNowProfessional | KyoshinMonitor | AccuracySupport | 受信データで更新、精度保持 |
| SignalNowProfessional | Axis | AccuracySupport | 受信データで更新、精度保持 |
| KyoshinMonitor | Axis | AccuracySupport | 受信データで更新、精度保持 |
| その他 | - | None | 更新しない |

## EEW Event Types

`EewEventType` enum（ワークフローシステムで使用）：

| イベント | 説明 |
|---------|------|
| `New` | 新規EEW受信 |
| `UpdateNewSerial` | 新しい報数の更新 |
| `UpdateWithMoreAccurate` | より正確なソースからの更新 |
| `Final` | 最終報 |
| `Cancel` | キャンセル報 |
| `WarningLevelReached` | 警報レベル到達 |
| `NewWarning` | 新規警報電文 |
| `UpdateWarning` | 警報電文更新 |
| `CancelWarning` | 警報キャンセル |
| `IncreaseMaxIntensity` | 最大震度上昇 |
| `DecreaseMaxIntensity` | 最大震度低下 |

## Timer-based Cleanup

`EewController.TimerElapsed()`:
- 3分経過したEEWを自動削除
- `EewCache`のEEW削除時、対応する`WarningEewCache`のエントリも連動削除
- 警報のみ（予報なし）の場合も3分で削除

## EewTelegramSubscriber

電文種別とメソッドの対応：

| 電文カテゴリ | 電文タイトル | 処理メソッド |
|------------|------------|-------------|
| `InformationCategory.EewForecast` | `緊急地震速報（地震動予報）` | `EewController.Update()` |
| `InformationCategory.EewWarning` | `緊急地震速報（警報）` | `EewController.UpdateWarning()` |

取消報の処理：
- 予報電文の取消: `EewController.Cancelled()`
- 警報電文の取消: `EewController.WarningCancelled()`

## InvokeEewUpdated

最終的なEEWリスト生成ロジック：
1. `EewCache`の全エントリを取得
2. 各エントリに対応する`WarningEewCache`の警報地域情報をマージ
3. `WarningEewCache`のみに存在するエントリを追加
4. `EewUpdated`イベントを発火
