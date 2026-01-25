---
name: eew-architecture
description: EEW（緊急地震速報）機能のアーキテクチャ解説。EEW関連のコード修正、予報電文・警報電文の処理フロー、キャッシュ管理、ソース優先順位の理解に使用。
user-invocable: false
---

# EEW (緊急地震速報) Architecture

## Instructions

EEW関連のコードを修正・理解する際は以下の手順に従う：

1. **変更対象の特定**: 予報電文か警報電文かを判断し、対応するキャッシュとメソッドを特定
2. **影響範囲の確認**: `EewController`の処理フローを追跡し、関連するイベントを確認
3. **ソース優先順位の考慮**: 複数ソース間の優先順位ルールを理解した上で修正
4. **テストデータの活用**: `EewMock.cs`でテストデータを確認・追加

## Key Files

| ファイル | 役割 |
|---------|------|
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/EewController.cs` | EEW統合管理コントローラ |
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Models/Eew.cs` | EEWデータモデル |
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Controls/EewPanel.axaml` | EEW表示UI |
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/EewTelegramSubscriber.cs` | 電文受信・振り分け |
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Services/Eew/SignalNowFileWatcher.cs` | SNPファイル監視 |
| `src/KyoshinEewViewer/Series/KyoshinMonitor/Models/EewMock.cs` | テスト用モックデータ |

## Dual Cache Architecture

`EewController`は2つの独立したキャッシュを管理：

| キャッシュ | 処理メソッド | 電文タイトル |
|-----------|-------------|-------------|
| `EewCache` | `Update()` | `緊急地震速報（地震動予報）` |
| `WarningEewCache` | `UpdateWarning()` | `緊急地震速報（警報）` |

**重要**: 予報電文と警報電文は**報数（SerialNo）が別々に管理**される。

## Processing Flow

```
[予報電文] → EewTelegramSubscriber → EewController.Update() → EewCache
[警報電文] → EewTelegramSubscriber → EewController.UpdateWarning() → WarningEewCache
    ↓
InvokeEewUpdated() → 両キャッシュをマージ → EewUpdated イベント発火
```

## Source Priority

`EewSource` enum と優先順位：
- **Dmdata**: 最優先（他ソースを上書き）
- **SignalNowProfessional**: 精度情報のみ補完
- **KyoshinMonitor / Axis**: 基本ソース

詳細なMixEewロジックは[reference.md](reference.md)を参照。

## Examples

**予報電文の新しい報数を追加する場合**:
1. `EewTelegramSubscriber`で電文を受信
2. `EewController.Update()`を呼び出し
3. `EewCache`に新規エントリ追加または既存を更新
4. `InvokeEewUpdated()`でイベント発火

**警報地域を追加表示する場合**:
1. `EewWarningAreas`モデルを確認（[reference.md](reference.md)参照）
2. `EewPanel.axaml`のバインディングを確認
3. 必要に応じて`EewController.UpdateWarning()`の処理を修正

## Additional Resources

詳細なモデル・イベント仕様は[reference.md](reference.md)を参照
