# dmdata冗長化実装計画

## 概要
現在のDmdataTelegramPublisherを参考にRedundantDmdataSocketControllerを使用したDmdataRedundantTelegramPublisherを実装する。

## 目的
- dmdata WebSocket接続の冗長性を提供
- 接続の安定性向上と単一障害点の解消
- ユーザーにはシンプルな設定インターフェースを提供
- 統計情報の可視化による運用性向上

## 現在のDmdataTelegramPublisherの機能分析

### 主要機能
1. **認証管理**: OAuth認証、リフレッシュトークンの管理
2. **接続管理**: WebSocketとPULL方式の自動切り替え
3. **再接続機能**: 接続断時の自動再接続とバックオフ
4. **カテゴリ管理**: 情報カテゴリの購読管理
5. **データ処理**: 電文の受信、キャッシュ、変換
6. **エラーハンドリング**: 接続エラー時のフォールバック処理

### 接続状態管理
- **ConnectionState**: 接続状態を表すenum
- **状態遷移**: 接続中→接続済み→切断中→未接続
- **同期制御**: SemaphoreSlimによる状態変更の同期

### フォールバック機能
- **WebSocket → PULL**: WebSocket接続失敗時にPULL方式に切り替え
- **再接続制御**: 指数バックオフによる再接続間隔制御
- **失敗回数制限**: 4回失敗でPULL方式に移行

## RedundantDmdataSocketControllerの機能分析

### 主要機能
1. **複数接続管理**: 東京・大阪エンドポイントへの同時接続
2. **冗長性制御**: 1つ以上の接続が生きていれば動作継続
3. **重複排除**: MessageDeduplicatorによる重複メッセージ除去
4. **自動再接続**: 個別接続の自動再接続機能
5. **統計情報**: 接続状態、メッセージ数、重複率の統計

### 冗長性状態
- **FullyConnected**: 全接続が正常
- **PartiallyConnected**: 過半数の接続が正常
- **Degraded**: 一部の接続が正常
- **Disconnected**: 全接続が切断

### イベント機能
- **DataReceived**: 重複排除後のデータ受信
- **ConnectionEstablished/Lost**: 個別接続の状態変化
- **RedundancyStatusChanged**: 冗長性状態の変化
- **AllConnectionsLost**: 全接続喪失

## 実装方針

### 1. 設定モデルの拡張
既存のDmdataConfigクラスに冗長性設定を追加：

```csharp
public class DmdataConfig : ReactiveObject
{
    // 既存の設定項目...
    
    private bool _useRedundancy = false;
    public bool UseRedundancy
    {
        get => _useRedundancy;
        set => this.RaiseAndSetIfChanged(ref _useRedundancy, value);
    }
}
```

### 2. DmdataRedundantTelegramPublisherの実装

#### 基本方針
- **既存のDmdataTelegramPublisherを継承**せず、独立したクラスとして実装
- **TelegramPublisher基底クラス**を継承
- **冗長性有効時**：RedundantDmdataSocketControllerを使用
- **冗長性無効時**：単一エンドポイントでRedundantDmdataSocketControllerを使用

#### 接続管理
- **冗長性有効**: 東京・大阪の両エンドポイントに接続
- **冗長性無効**: 東京エンドポイントのみに接続
- **フォールバック**: RedundantDmdataSocketController内の再接続機能に依存

#### PULL方式フォールバック条件
RedundantDmdataSocketControllerの全接続が失敗した場合：
1. **AllConnectionsLost**イベントを受信
2. **一定時間経過**後もRedundancyStatusがDisconnectedの場合
3. **既存のPULL方式**に自動切り替え

#### 統計情報の管理
- **接続状態**: RedundancyStatus
- **アクティブ接続数**: ActiveConnectionCount
- **接続エンドポイント**: ConnectedEndpoints
- **メッセージ統計**: TotalMessagesReceived, DuplicateMessagesFiltered
- **最終受信時刻**: LastMessageTime

### 3. 設定画面の統計情報表示
DmdataPublisherの設定ページに統計情報パネルを追加：

#### 表示項目
- **冗長性状態**: FullyConnected/PartiallyConnected/Degraded/Disconnected
- **接続状況**: アクティブ接続数/総接続数
- **エンドポイント**: 接続中のエンドポイント一覧
- **メッセージ統計**: 受信数、重複除去数、重複率
- **最終受信時刻**: 最後にメッセージを受信した時刻

#### UI実装
- **リアルタイム更新**: ReactiveUIによる自動更新
- **視覚的表示**: 接続状態のアイコン表示
- **詳細情報**: 展開可能な統計パネル

### 4. 実装手順

#### Phase 1: 基本実装
1. **DmdataRedundantTelegramPublisher**クラスの作成
2. **RedundantDmdataSocketController**の統合
3. **基本的な接続管理**機能の実装
4. **設定との連携**機能の実装

#### Phase 2: フォールバック実装
1. **PULL方式フォールバック**の実装
2. **状態管理**の統合
3. **エラーハンドリング**の実装
4. **再接続ロジック**の調整

#### Phase 3: 統計情報表示
1. **統計情報収集**機能の実装
2. **設定画面UI**の拡張
3. **リアルタイム更新**の実装
4. **視覚的フィードバック**の追加

### 5. 設計上の考慮事項

#### ユーザビリティ
- **シンプルな設定**: 冗長性の有効/無効のみ
- **自動動作**: 障害時の自動フォールバック
- **透明性**: 内部動作の可視化

#### 互換性
- **既存設定**: 現在のDmdataConfigとの互換性維持
- **段階的移行**: 既存ユーザーの設定移行サポート
- **フォールバック**: 既存のPULL方式への後方互換性

#### 拡張性
- **エンドポイント追加**: 将来的なエンドポイント拡張に対応
- **メトリクス**: 監視・運用に必要な統計情報の拡張
- **設定項目**: 高度な設定項目の段階的追加

## 期待される効果

### 可用性向上
- **単一障害点解消**: 複数エンドポイントによる冗長性
- **自動復旧**: 接続断時の自動再接続
- **継続運用**: 一部障害時の運用継続

### 運用性向上
- **状態監視**: リアルタイムの接続状態表示
- **統計情報**: メッセージ受信の統計と重複率
- **障害検知**: 接続問題の早期発見

### ユーザー体験向上
- **透明性**: 内部動作の可視化
- **信頼性**: 安定した電文受信
- **シンプルさ**: 複雑な設定の隠蔽

## 実装上の注意点

### パフォーマンス
- **リソース使用量**: 複数接続によるメモリ・CPU使用量増加
- **ネットワーク負荷**: 重複メッセージ受信による帯域使用量
- **重複排除**: 効率的な重複メッセージ処理

### 障害対応
- **部分障害**: 一部エンドポイントの障害時の動作
- **全体障害**: 全エンドポイント障害時のフォールバック
- **ネットワーク障害**: 一時的な接続断への対応

### 設定管理
- **デフォルト値**: 安全なデフォルト設定
- **設定変更**: 運用中の設定変更への対応
- **設定保存**: 設定の永続化と復元

この実装計画に基づいて、段階的にDmdataRedundantTelegramPublisherを実装し、dmdata接続の冗長性と運用性を向上させる。

## 実装完了状況

### ✅ 完了した項目
- **基本実装**: DmdataRedundantTelegramPublisherクラスの作成
- **冗長性設定**: UseRedundancy設定項目の追加
- **統計情報表示**: 設定画面での接続状態・統計情報表示
- **リソース管理**: Disposeパターンの実装
- **例外処理**: イベントハンドラでの適切な例外処理
- **フォールバック**: WebSocket→PULL自動切り替え

### ⚠️ 注意事項
- **DIコンテナ登録**: 本実装を使用する場合はDIコンテナへの登録が必要
- **従来実装との共存**: 既存のDmdataTelegramPublisherとの使い分けが必要
- **テスト**: 本格運用前に接続テストと設定変更テストを推奨

### 🔧 運用上の推奨事項
- 冗長性機能は段階的に有効化し、動作確認を行うこと
- 統計情報を定期的に確認し、接続状況を監視すること
- ログを確認し、異常な再接続パターンがないかチェックすること