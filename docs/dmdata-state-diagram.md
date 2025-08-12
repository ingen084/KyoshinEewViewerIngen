# DmdataRedundantTelegramPublisher 状態遷移図

## ConnectionState 状態遷移図

```mermaid
stateDiagram-v2
    [*] --> Disconnected: 初期状態
    
    Disconnected --> Connecting: StartWebSocketAsync()/StartPullAsync()
    
    Connecting --> WebSocketConnected: WebSocket接続成功
    Connecting --> PullConnected: PULL接続成功
    Connecting --> Disconnected: 接続失敗（復旧可能）
    Connecting --> Failed: 認証エラー
    
    WebSocketConnected --> Disconnecting: Stop()/切断要求
    WebSocketConnected --> Disconnected: 全接続喪失（FailCount < 3）
    WebSocketConnected --> PullConnected: 全接続喪失（FailCount >= 3）
    WebSocketConnected --> TemporaryFailure: 一時的な障害
    
    PullConnected --> Disconnecting: Stop()/切断要求
    PullConnected --> TemporaryFailure: 一時的な障害
    PullConnected --> Failed: 認証エラー
    
    Disconnecting --> Disconnected: 切断完了
    
    TemporaryFailure --> Connecting: 復旧試行（TemporaryFailureRecoveryTimer）
    TemporaryFailure --> Failed: 認証エラー検出
    
    Failed --> [*]: 完全失敗（認可情報削除）
    
    Disconnected --> Connecting: WebSocketReconnectTimer発火

    note right of WebSocketConnected
        - RedundantController接続中
        - リアルタイム電文受信
        - 複数エンドポイント冗長化
    end note
    
    note right of PullConnected
        - HTTP APIでポーリング
        - PullTimerで定期取得
        - EEW非対応
    end note
    
    note right of TemporaryFailure
        - 認証情報は保持
        - 指数バックオフで再試行
        - WebSocketReconnectTimer停止
    end note
    
    note right of Failed
        - 認証エラー発生
        - RefreshToken削除
        - 復旧不可能
    end note
```

## タイマー制御の状態図

```mermaid
stateDiagram-v2
    state "タイマー管理" as TimerControl {
        state "WebSocketReconnectTimer" as WSTimer {
            [*] --> WST_Active: 初期化（10秒後開始）
            WST_Active --> WST_Checking: タイマー発火
            WST_Checking --> WST_Active: 再接続不要
            WST_Checking --> WST_Reconnecting: 再接続条件満たす
            WST_Reconnecting --> WST_Active: StartInternalAsync()実行
            WST_Active --> WST_Stopped: TemporaryFailure移行
            WST_Stopped --> WST_Active: 復旧成功
        }
        
        state "TemporaryFailureRecoveryTimer" as TFTimer {
            [*] --> TFT_Inactive: 初期状態
            TFT_Inactive --> TFT_Scheduled: TemporaryFailAsync()
            TFT_Scheduled --> TFT_Executing: タイマー発火
            TFT_Executing --> TFT_Inactive: 復旧成功
            TFT_Executing --> TFT_Scheduled: 復旧失敗（再スケジュール）
        }
        
        state "PullTimer" as PTimer {
            [*] --> PT_Inactive: 初期状態
            PT_Inactive --> PT_Active: PullConnected
            PT_Active --> PT_Fetching: タイマー発火
            PT_Fetching --> PT_Active: PullFeedAsync()完了
            PT_Active --> PT_Inactive: 接続切断/TemporaryFailure
        }
    }
    
    note right of WSTimer
        条件：
        - ApiClient != null
        - SubscribingCategories.Any()
        - UseWebSocket == true
        - RedundancyStatus == Disconnected
        - CurrentState != Connecting
        - CurrentState != Disconnecting
        - CurrentState != TemporaryFailure
    end note
    
    note right of TFTimer
        再試行間隔（指数バックオフ）：
        - 1回目: 10秒
        - 2回目: 20秒
        - 3回目: 40秒
        - 4回目: 80秒
        - 5回目: 160秒
        - 6回目以降: 300秒（最大）
    end note
```

## 冗長性状態（RedundancyStatus）の遷移

```mermaid
stateDiagram-v2
    [*] --> Disconnected: 初期状態
    
    Disconnected --> Connecting: WebSocket接続開始
    
    Connecting --> Connected: 1つ以上のエンドポイント接続成功
    Connecting --> Disconnected: 全接続失敗
    
    Connected --> PartiallyConnected: 一部エンドポイント切断
    Connected --> Disconnected: 全エンドポイント切断
    
    PartiallyConnected --> Connected: エンドポイント復旧
    PartiallyConnected --> Disconnected: 残りも切断
    
    note right of Connected
        ActiveConnectionCount >= 2
        全エンドポイント接続
    end note
    
    note right of PartiallyConnected
        ActiveConnectionCount == 1
        一部エンドポイントのみ接続
    end note
```

## エラー処理と復旧の判定フロー

```mermaid
flowchart TD
    Start([エラー発生]) --> CheckAuth{認証エラー？}
    
    CheckAuth -->|Yes| AuthError[認証エラー処理]
    CheckAuth -->|No| CheckRetryable{復旧可能？}
    
    AuthError --> FailAsync[FailAsync実行]
    FailAsync --> DeleteToken[RefreshToken削除]
    DeleteToken --> NotifyFailed[OnFailed<br/>isRestorable=false]
    NotifyFailed --> End1([Failed状態へ遷移])
    
    CheckRetryable -->|Yes| TempFail[TemporaryFailAsync実行]
    CheckRetryable -->|No| DirectFail[直接失敗処理]
    
    TempFail --> IncCounter[TemporaryFailureCount++]
    IncCounter --> StopTimers[タイマー停止]
    StopTimers --> NotifyTemp[OnFailed<br/>isRestorable=false]
    NotifyTemp --> CalcBackoff[指数バックオフ計算]
    CalcBackoff --> SetRetryTimer[TemporaryFailureRecoveryTimer設定]
    SetRetryTimer --> End2([TemporaryFailure状態へ遷移])
    
    DirectFail --> NotifyDirect[OnFailed<br/>isRestorable=true/false]
    NotifyDirect --> End3([Disconnected状態へ遷移])
```

## WebSocket/PULL切り替えロジック

```mermaid
flowchart TD
    Start([StartInternalAsync]) --> CheckWS{UseWebSocket?}
    
    CheckWS -->|Yes| StartWS[StartWebSocketAsync]
    CheckWS -->|No| StartPull[StartPullAsync]
    
    StartWS --> WSConnect[WebSocket接続試行]
    WSConnect --> WSSuccess{接続成功？}
    
    WSSuccess -->|Yes| WSConnected[WebSocketConnected状態]
    WSSuccess -->|No| WSError{認証エラー？}
    
    WSError -->|Yes| WSAuthFail[Failed状態]
    WSError -->|No| WSFallback[PULLへフォールバック]
    
    WSFallback --> StartPull
    
    StartPull --> CheckCategories{PULL可能な<br/>カテゴリあり？}
    
    CheckCategories -->|Yes| PullConnect[PullConnected状態]
    CheckCategories -->|No| Disconnected[Disconnected状態]
    
    WSConnected --> SendHistory1[SwitchInformationAsync<br/>true]
    PullConnect --> SendHistory2[SwitchInformationAsync<br/>false]
    
    SendHistory1 --> CheckRecovery{TemporaryFailure<br/>からの復旧？}
    SendHistory2 --> CheckRecovery
    
    CheckRecovery -->|Yes| NotifyRecovery[OnInformationCategoryUpdated]
    CheckRecovery -->|No| End([完了])
    
    NotifyRecovery --> RestartWSTimer[WebSocketReconnectTimer再開]
    RestartWSTimer --> End
```

## 主要な状態遷移ルール

### 1. **Disconnected → Connecting**
- トリガー: `StartWebSocketAsync()` または `StartPullAsync()`
- 条件: `ApiClient != null`

### 2. **Connecting → WebSocketConnected/PullConnected**
- トリガー: 接続成功
- アクション: `SwitchInformationAsync()`実行

### 3. **WebSocketConnected → TemporaryFailure**
- トリガー: 認証以外のエラー
- アクション: 
  - `WebSocketReconnectTimer`停止
  - `TemporaryFailureRecoveryTimer`開始
  - フォールバック通知

### 4. **TemporaryFailure → Connecting**
- トリガー: `TemporaryFailureRecoveryTimer`発火
- アクション: `StartInternalAsync()`実行

### 5. **Any → Failed**
- トリガー: 認証エラー（401系）
- アクション:
  - 全タイマー停止
  - RefreshToken削除
  - 復旧不可能通知

## 状態遷移時のタイマー制御

| 現在の状態 | 次の状態 | WebSocketReconnectTimer | TemporaryFailureRecoveryTimer | PullTimer |
|-----------|---------|-------------------------|------------------------------|-----------|
| Disconnected | Connecting | 継続 | 停止 | 停止 |
| Connecting | WebSocketConnected | 継続 | 停止 | 停止 |
| Connecting | PullConnected | 継続 | 停止 | 開始 |
| WebSocketConnected | TemporaryFailure | **停止** | 開始 | 停止 |
| PullConnected | TemporaryFailure | **停止** | 開始 | 停止 |
| TemporaryFailure | Connecting | 停止維持 | 継続 | 停止 |
| TemporaryFailure復旧 | Connected | **再開** | 停止 | 状態による |
| Any | Failed | 停止 | 停止 | 停止 |

## 重要なポイント

1. **TemporaryFailure中はWebSocketReconnectTimerを停止**
   - 復旧メカニズムの競合を防止
   - TemporaryFailureRecoveryTimerのみが動作

2. **復旧成功時にWebSocketReconnectTimerを再開**
   - 将来の切断に備える
   - 通常の再接続メカニズムを有効化

3. **認証エラーは即座にFailed状態へ**
   - 復旧試行しない
   - ユーザーの再認証が必要

4. **指数バックオフによる再試行**
   - TemporaryFailureでの再試行間隔を段階的に延長
   - サーバー負荷軽減