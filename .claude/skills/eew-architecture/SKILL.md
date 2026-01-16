---
name: eew-architecture
description: EEW（緊急地震速報）機能のアーキテクチャ解説。EEW関連のコード修正、予報電文・警報電文の処理フロー、キャッシュ管理、ソース優先順位の理解に使用。
user-invocable: false
---

# EEW (緊急地震速報) Architecture

EEW機能は`KyoshinMonitor`シリーズ内で実装されており、複数のソースからの緊急地震速報を統合管理する。

## Key Files

- **Controller**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/EewController.cs`
- **Model**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Models/Eew.cs`
- **UI**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Controls/EewPanel.axaml`
- **Telegram Subscriber**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/EewTelegramSubscriber.cs`
- **SNP File Watcher**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/SignalNowFileWatcher.cs`
- **Mock Data**: `src/KyoshinEewViewer/Series/KyoshinMonitor/Models/EewMock.cs`

## EEW Data Sources

`EewSource` enum:
- `KyoshinMonitor`: 強震モニタ
- `SignalNowProfessional`: SignalNow Professional
- `Dmdata`: DM-D.S.S (dmdata.jp)
- `Axis`: AXIS

## Dual Cache Architecture

`EewController`は2つの独立したキャッシュを管理。詳細は[reference.md](reference.md)を参照。

### 1. EewCache（予報電文）
- `Update()` メソッドで処理
- 電文タイトル: `緊急地震速報（地震動予報）`

### 2. WarningEewCache（警報電文）
- `UpdateWarning()` メソッドで処理
- 電文タイトル: `緊急地震速報（警報）`

**重要**: 予報電文と警報電文は**報数（SerialNo）が別々に管理**される。

## Processing Flow

```
[予報電文] → EewTelegramSubscriber → EewController.Update() → EewCache
[警報電文] → EewTelegramSubscriber → EewController.UpdateWarning() → WarningEewCache
    ↓
InvokeEewUpdated() → 両キャッシュをマージ → EewUpdated イベント発火
```

## Source Priority (MixEew)

同じ報数で異なるソースから受信した場合：
- **Dmdata**が最優先（他ソースを上書き）
- **SNP**は精度情報のみ補完
- 報数が新しければ問答無用で更新
- 報数が古ければ無視

## UI Display

`EewPanel.axaml`の構成：
- ヘッダー: 報数・最終報表示
- 本文: 発生時刻、震央地名、深さ、M、最大震度
- 精度情報: 震央/深さ/規模の精度フラグ
- 警報地域: 地域名一覧と警報受信元（報数含む）
- フッター: 受信元

## Additional Resources

- 詳細なモデル・イベント仕様は[reference.md](reference.md)を参照
