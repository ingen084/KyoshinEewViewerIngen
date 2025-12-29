# ShakeDetectionProducer Valkey Stream Payload 仕様書

## 概要

ShakeDetectionProducerは、強震モニタの揺れ検知イベントをValkey Streamに送信します。
すべてのペイロードはJSON形式でシリアライズされ、`type`フィールドで識別されます。

## Stream設定

- **デフォルトStream Key**: `shake-detect-events`
- **環境変数**: `VALKEY_STREAM_KEY`
- **最大保持メッセージ数**: 10000 (環境変数: `VALKEY_STREAM_MAXLEN`)

## メッセージ形式

各メッセージは以下のフィールドを持つ Redis Stream Entry として送信されます：

| フィールド | 説明 |
|-----------|------|
| `eventId` | イベント識別子 (shake_detected: UUID, error: `error-{yyyyMMddHHmmss}`) |
| `type` | ペイロードタイプ (`shake_detected` \| `error`) |
| `payload` | JSON形式のペイロード本体 |

---

## ペイロード型

### 基底型: `StreamPayload`

すべてのペイロードは `type` フィールドを持ち、ペイロードの種類を識別します。

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `type` | `string` | ペイロードタイプ識別子 (`shake_detected` \| `error`) |

---

## 1. ShakeDetectedPayload

揺れ検知イベントを表すペイロードです。

### フィールド一覧

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `type` | `string` | ✓ | 固定値: `"shake_detected"` |
| `eventId` | `string` (UUID) | ✓ | イベントの一意識別子 |
| `createdAt` | `string` (ISO 8601) | ✓ | イベント作成日時 |
| `level` | `'Weaker' \| 'Weak' \| 'Medium' \| 'Strong' \| 'Stronger'` | ✓ | 揺れレベル |
| `isLevelUp` | `boolean` | ✓ | レベルが上昇したか |
| `isReplay` | `boolean` | ✓ | リプレイイベントか |
| `pointCount` | `integer` | ✓ | 検知した観測点数 |
| `region` | `RegionPayload` | ✓ | 検知領域 |
| `points` | `ObservationPointPayload[]` | ✓ | 観測点情報の配列 |

### Level値

|名前|揺れの強さ|
|:--|:--|
|`Weaker`|微弱な揺れ|
|`Weak`|弱い揺れ(震度1未満)|
|`Medium`|揺れ(震度1以上)|
|`Strong`|強い揺れ(震度3程度以上)|
|`Stronger`|非常に強い揺れ(震度5弱程度以上)|

### サンプル

```json
{
  "type": "shake_detected",
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-12-28T12:34:56.789Z",
  "level": "Medium",
  "isLevelUp": true,
  "isReplay": false,
  "pointCount": 15,
  "region": {
    "topLeft": {
      "latitude": 35.6895,
      "longitude": 139.6917
    },
    "bottomRight": {
      "latitude": 35.4895,
      "longitude": 139.8917
    }
  },
  "points": [
    {
      "code": "TKY001",
      "name": "東京",
      "region": "関東",
      "type": "KiKnet",
      "location": {
        "latitude": 35.6895,
        "longitude": 139.6917
      },
      "intensity": 2.5,
      "intensityDiff": 0.3
    }
  ]
}
```

---

## 2. ErrorPayload

エラーイベントを表すペイロードです。

### フィールド一覧

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `type` | `string` | ✓ | 固定値: `"error"` |
| `errorType` | `string` | ✓ | エラー種別 |
| `time` | `string` (ISO 8601) | ✓ | エラー発生日時 |
| `message` | `string` | ✓ | エラーメッセージ |

### ErrorType値

| errorType | 説明 |
|-----------|------|
| `timeout` | 強震モニタからのデータ取得がタイムアウト |
| `http_error` | HTTPエラーが発生 |
| `parse_error` | データ解析エラーが発生 |

### サンプル

```json
{
  "type": "error",
  "errorType": "timeout",
  "time": "2024-12-28T12:34:56.789Z",
  "message": "強震モニタからのデータ取得がタイムアウトしました"
}
```

---

## 補助型

### RegionPayload

揺れ検知領域を表す矩形領域です。

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `topLeft` | `LocationPayload` | ✓ | 左上座標 |
| `bottomRight` | `LocationPayload` | ✓ | 右下座標 |

### LocationPayload

座標を表します。

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `latitude` | `number` (float) | ✓ | 緯度 (度) |
| `longitude` | `number` (float) | ✓ | 経度 (度) |

### ObservationPointPayload

観測点情報を表します。

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `code` | `string` | ✓ | 観測点コード |
| `name` | `string` | ✓ | 観測点名 |
| `region` | `string` | ✓ | 地域名 |
| `type` | `string` | ✓ | 観測点種別 (`KiKnet`, `JMA` など) |
| `location` | `LocationPayload` | ✓ | 観測点座標 |
| `intensity` | `number?` (double) | ✓ | 最新の震度値 (null可) |
| `intensityDiff` | `number` (double) | ✓ | 震度変化量 |

---

## コンシューマーグループの使用

Valkey Streamはコンシューマーグループをサポートしており、複数のコンシューマーで負荷分散や at-least-once 配信保証が可能です。

### グループの作成

```bash
# コンシューマーグループを作成（Streamが存在しない場合はMKSTREAMで作成）
valkey-cli XGROUP CREATE shake-detect-events my-group $ MKSTREAM
```

### メッセージの読み取り

```bash
# グループからメッセージを読み取り
valkey-cli XREADGROUP GROUP my-group consumer1 COUNT 10 STREAMS shake-detect-events >

# 処理完了後にACK
valkey-cli XACK shake-detect-events my-group <message-id>
```

---

## TypeScript型定義

```typescript
type StreamPayload = ShakeDetectedPayload | ErrorPayload;

interface ShakeDetectedPayload {
  type: "shake_detected";
  eventId: string;
  createdAt: string;
  level: 'Weaker' | 'Weak' | 'Medium' | 'Strong' | 'Stronger';
  isLevelUp: boolean;
  isReplay: boolean;
  pointCount: number;
  region: RegionPayload;
  points: ObservationPointPayload[];
}

interface ErrorPayload {
  type: "error";
  errorType: "timeout" | "http_error" | "parse_error";
  time: string;
  message: string;
}

interface RegionPayload {
  topLeft: LocationPayload;
  bottomRight: LocationPayload;
}

interface LocationPayload {
  latitude: number;
  longitude: number;
}

interface ObservationPointPayload {
  code: string;
  name: string;
  region: string;
  type: string;
  location: LocationPayload;
  intensity: number | null;
  intensityDiff: number;
}
```

---

## Valkey CLI コマンド例

### Streamの確認

```bash
# 最新のメッセージを取得
valkey-cli XREVRANGE shake-detect-events + - COUNT 5

# Streamの情報を取得
valkey-cli XINFO STREAM shake-detect-events

# Streamの長さを取得
valkey-cli XLEN shake-detect-events
```

### リアルタイム監視

```bash
# 新しいメッセージをブロッキングで待機
valkey-cli XREAD BLOCK 0 STREAMS shake-detect-events $
```
