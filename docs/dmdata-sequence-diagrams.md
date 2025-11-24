# DmdataRedundantTelegramPublisher シーケンス図

## 1. 正常な復旧フロー

```mermaid
sequenceDiagram
    participant DRP as DmdataRedundantTelegramPublisher
    participant TPS as TelegramProvideService
    participant Sub as Subscriber
    participant WS as WebSocket
    participant API as Dmdata API

    Note over DRP: TemporaryFailure状態から復旧
    
    DRP->>DRP: StartInternalAsync()
    DRP->>DRP: WebSocketReconnectTimer停止
    DRP->>DRP: TemporaryFailureRecoveryTimer停止
    
    alt WebSocket接続の場合
        DRP->>DRP: StartWebSocketAsync()
        DRP->>WS: 接続開始
        WS-->>DRP: 接続成功
        DRP->>DRP: CurrentState = WebSocketConnected
        DRP->>API: FetchListAsync(カテゴリごと)
        API-->>DRP: 履歴データ
        DRP->>DRP: SwitchInformationAsync(true)
    else PULL接続の場合
        DRP->>DRP: StartPullAsync()
        DRP->>DRP: CurrentState = PullConnected
        DRP->>API: FetchListAsync(カテゴリごと)
        API-->>DRP: 履歴データ
        DRP->>DRP: SwitchInformationAsync(false)
    end
    
    Note over DRP: 復旧成功時
    DRP->>DRP: TemporaryFailureCount = 0
    DRP->>TPS: OnInformationCategoryUpdated()
    
    TPS->>TPS: プロバイダの優先度を再評価
    TPS->>TPS: 高優先度プロバイダに切り替え
    
    DRP->>TPS: OnHistoryTelegramArrived(カテゴリごと)
    TPS->>Sub: SourceSwitched(name, telegrams)
    
    DRP->>DRP: WebSocketReconnectTimer再開
```

## 2. 一時的障害（TemporaryFailure）の処理フロー

```mermaid
sequenceDiagram
    participant DRP as DmdataRedundantTelegramPublisher
    participant TPS as TelegramProvideService
    participant Timer as Timers
    participant API as Dmdata API

    Note over DRP: 認証以外のエラー発生
    
    DRP->>DRP: TemporaryFailAsync(reason)
    DRP->>DRP: CurrentState = TemporaryFailure
    DRP->>DRP: TemporaryFailureCount++
    
    DRP->>Timer: PullTimer停止
    DRP->>Timer: WebSocketReconnectTimer停止
    DRP->>TPS: OnFailed(categories, isRestorable=false)
    
    TPS->>TPS: フォールバックプロバイダに切り替え
    
    DRP->>DRP: 現在の接続を切断
    DRP->>DRP: 指数バックオフで再試行間隔計算
    Note over DRP: 10秒 → 20秒 → 40秒... 最大300秒
    
    DRP->>Timer: TemporaryFailureRecoveryTimer設定
    
    Note over Timer: 待機期間
    
    Timer-->>DRP: タイマー発火
    DRP->>DRP: StartInternalAsync()で復旧試行
```

## 3. 複数カテゴリーの履歴送信フロー

```mermaid
sequenceDiagram
    participant DRP as DmdataRedundantTelegramPublisher
    participant TPS as TelegramProvideService
    participant Sub as Subscriber
    participant API as Dmdata API

    Note over DRP: SwitchInformationAsync実行
    
    loop カテゴリごとに処理
        alt EEW系カテゴリ（WebSocketのみ）
            DRP->>TPS: OnHistoryTelegramArrived(name, category, [])
            Note over TPS: 空配列を送信
        else その他のカテゴリ
            DRP->>API: FetchListAsync(category, false)
            API-->>DRP: 履歴電文リスト
            DRP->>TPS: OnHistoryTelegramArrived(name, category, telegrams)
        end
        
        TPS->>TPS: UsingPublisher確認
        alt 現在使用中のプロバイダからの場合
            TPS->>Sub: SourceSwitched(name, telegrams)
        else 使用中でないプロバイダからの場合
            TPS->>TPS: 無視
        end
        
        DRP->>DRP: await Task.Delay(interval)
        Note over DRP: 次のカテゴリまで待機
    end
```

## 4. タイマー競合の防止フロー

```mermaid
sequenceDiagram
    participant WST as WebSocketReconnectTimer
    participant TFT as TemporaryFailureRecoveryTimer
    participant DRP as DmdataRedundantTelegramPublisher
    participant State as ConnectionState

    Note over DRP: 初期状態: Disconnected
    
    WST->>DRP: 再接続チェック（10秒ごと）
    DRP->>State: CurrentState確認
    
    alt TemporaryFailure状態の場合
        DRP->>WST: 何もしない（スキップ）
        Note over WST: TemporaryFailure中は動作しない
    else Disconnected状態の場合
        DRP->>DRP: StartInternalAsync()
        Note over DRP: WebSocket再接続試行
    end
    
    Note over DRP: 別のタイミングで障害発生
    
    DRP->>DRP: TemporaryFailAsync()
    DRP->>State: CurrentState = TemporaryFailure
    DRP->>WST: Change(Infinite, Infinite)
    Note over WST: 明示的に停止
    
    DRP->>TFT: 再試行タイマー設定
    
    TFT-->>DRP: タイマー発火
    DRP->>DRP: 復旧試行
    
    alt 復旧成功
        DRP->>State: CurrentState = WebSocketConnected/PullConnected
        DRP->>WST: Change(ReconnectBackoffTime, Infinite)
        Note over WST: 再接続タイマー再開
    else 復旧失敗
        DRP->>State: CurrentState = TemporaryFailure維持
        Note over WST: 停止状態を維持
    end
```

## 5. プロバイダ優先度による切り替えフロー

```mermaid
sequenceDiagram
    participant P1 as Primary Publisher (優先度1)
    participant P2 as Secondary Publisher (優先度2)
    participant TPS as TelegramProvideService
    participant Sub as Subscriber

    Note over TPS: 初期状態: P1が障害でP2が処理中
    
    P1->>P1: 復旧処理完了
    P1->>TPS: OnInformationCategoryUpdated()
    
    TPS->>TPS: OnInformationCategoryUpdated処理
    TPS->>P1: GetSupportedCategoriesAsync()
    P1-->>TPS: [Earthquake, Tsunami]
    TPS->>P2: GetSupportedCategoriesAsync()
    P2-->>TPS: [Earthquake, Tsunami]
    
    TPS->>TPS: 優先度評価
    Note over TPS: P1の優先度 < P2の優先度
    
    TPS->>TPS: UsingPublisher更新
    TPS->>P1: Start([Earthquake, Tsunami])
    TPS->>P2: Stop([Earthquake, Tsunami])
    
    P1->>TPS: OnHistoryTelegramArrived(Earthquake)
    TPS->>Sub: SourceSwitched("Primary", earthquakeTelegrams)
    
    P1->>TPS: OnHistoryTelegramArrived(Tsunami)
    TPS->>Sub: SourceSwitched("Primary", tsunamiTelegrams)
```

## 主な改善点

### 1. タイマー競合の防止
- `TemporaryFailure`状態では`WebSocketReconnectTimer`が動作しない
- 復旧成功時にのみ`WebSocketReconnectTimer`を再開

### 2. 履歴送信の順序保証
- `OnInformationCategoryUpdated`を先に呼び出し
- その後`OnHistoryTelegramArrived`を呼び出す

### 3. 複数カテゴリーの処理
- カテゴリーごとに順次処理
- 各カテゴリー間で適切な遅延を設定

## 注意事項

1. **履歴データの重複送信**
   - 複数のプロバイダが同時に復旧した場合、短時間に複数の履歴データが送信される可能性がある
   - `TelegramProvideService`側でUsingPublisherチェックにより、適切なプロバイダからのデータのみを処理

2. **カテゴリーごとの処理遅延**
   - `SwitchInformationAsync`内で各カテゴリー処理後に`await Task.Delay(interval)`を実行
   - これにより連続的な履歴送信を緩和

3. **状態遷移の管理**
   - `ConnectionState`により接続状態を厳密に管理
   - 不適切な状態での処理をスキップ