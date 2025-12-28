# ShakeDetectionProducer Kafka Payload 仕様書

## 概要

ShakeDetectionProducerは、強震モニタの揺れ検知イベントをKafkaに送信します。
すべてのペイロードはJSON形式でシリアライズされ、`type`フィールドで識別されます。

## トピック

- **デフォルトトピック名**: `shake-detect-events`
- **環境変数**: `KAFKA_TOPIC`

## メッセージキー

| ペイロードタイプ | キー形式 |
|-----------------|---------|
| `shake_detected` | `{EventId}` (UUID) |
| `error` | `error-{yyyyMMddHHmmss}` |

---

## ペイロード型

### 基底型: `KafkaPayload`

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
| `level` | `string` | ✓ | 揺れレベル (文字列表現) |
| `levelValue` | `integer` | ✓ | 揺れレベル (数値) |
| `isLevelUp` | `boolean` | ✓ | レベルが上昇したか |
| `isReplay` | `boolean` | ✓ | リプレイイベントか |
| `pointCount` | `integer` | ✓ | 検知した観測点数 |
| `region` | `RegionPayload` | ✓ | 検知領域 |
| `points` | `ObservationPointPayload[]` | ✓ | 観測点情報の配列 |

### Level値

| levelValue | level | 説明 |
|:----------:|-------|------|
| 0 | `Unknown` | 不明 |
| 1 | `Weak` | 弱い揺れ |
| 2 | `Medium` | 中程度の揺れ |
| 3 | `Strong` | 強い揺れ |
| 4 | `VeryStrong` | 非常に強い揺れ |

### サンプル

```json
{
  "type": "shake_detected",
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-12-28T12:34:56.789Z",
  "level": "Medium",
  "levelValue": 2,
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

## TypeScript型定義

```typescript
type KafkaPayload = ShakeDetectedPayload | ErrorPayload;

interface ShakeDetectedPayload {
  type: "shake_detected";
  eventId: string;
  createdAt: string;
  level: string;
  levelValue: number;
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

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "oneOf": [
    { "$ref": "#/definitions/ShakeDetectedPayload" },
    { "$ref": "#/definitions/ErrorPayload" }
  ],
  "definitions": {
    "ShakeDetectedPayload": {
      "type": "object",
      "required": ["type", "eventId", "createdAt", "level", "levelValue", "isLevelUp", "isReplay", "pointCount", "region", "points"],
      "properties": {
        "type": { "const": "shake_detected" },
        "eventId": { "type": "string", "format": "uuid" },
        "createdAt": { "type": "string", "format": "date-time" },
        "level": { "type": "string" },
        "levelValue": { "type": "integer" },
        "isLevelUp": { "type": "boolean" },
        "isReplay": { "type": "boolean" },
        "pointCount": { "type": "integer" },
        "region": { "$ref": "#/definitions/RegionPayload" },
        "points": { "type": "array", "items": { "$ref": "#/definitions/ObservationPointPayload" } }
      }
    },
    "ErrorPayload": {
      "type": "object",
      "required": ["type", "errorType", "time", "message"],
      "properties": {
        "type": { "const": "error" },
        "errorType": { "type": "string", "enum": ["timeout", "http_error", "parse_error"] },
        "time": { "type": "string", "format": "date-time" },
        "message": { "type": "string" }
      }
    },
    "RegionPayload": {
      "type": "object",
      "required": ["topLeft", "bottomRight"],
      "properties": {
        "topLeft": { "$ref": "#/definitions/LocationPayload" },
        "bottomRight": { "$ref": "#/definitions/LocationPayload" }
      }
    },
    "LocationPayload": {
      "type": "object",
      "required": ["latitude", "longitude"],
      "properties": {
        "latitude": { "type": "number" },
        "longitude": { "type": "number" }
      }
    },
    "ObservationPointPayload": {
      "type": "object",
      "required": ["code", "name", "region", "type", "location", "intensity", "intensityDiff"],
      "properties": {
        "code": { "type": "string" },
        "name": { "type": "string" },
        "region": { "type": "string" },
        "type": { "type": "string" },
        "location": { "$ref": "#/definitions/LocationPayload" },
        "intensity": { "type": ["number", "null"] },
        "intensityDiff": { "type": "number" }
      }
    }
  }
}
```
