# ShakeDetectionProducer

強震モニタの観測点データを取得し、揺れ検知イベントを Valkey Stream に送信するサービスです。

## クイックスタート

```bash
cd src/Sandboxes/ShakeDetectionProducer
docker compose up -d
```

## Docker Compose

### ビルドと起動

```bash
# ビルドして起動
docker compose up -d --build

# 別の観測点データタグを指定してビルド
docker compose build --build-arg OBSERVATION_POINTS_TAG=v2025.12.27
docker compose up -d
```

### ビルド引数

| 引数 | デフォルト値 | 説明 |
|------|-------------|------|
| `OBSERVATION_POINTS_TAG` | `v2025.12.27` | [kyoshin-monitor-observation-points](https://github.com/ingen084/kyoshin-monitor-observation-points) のリリースタグ |

### 環境変数

環境変数は `.env` ファイルまたはシェルで設定できます。

| 環境変数 | デフォルト値 | 説明 |
|----------|-------------|------|
| `OBSERVATION_POINTS_PATH` | `/app/observation-points.kmop` | 観測点データファイルのパス（イメージに含まれています） |
| `TIMER_OFFSET_MS` | `1100` | タイマーオフセット（ミリ秒） |
| `VALKEY_CONNECTION_STRING` | `valkey:6379` | Valkey 接続文字列 |
| `VALKEY_STREAM_KEY` | `shake-detect-events` | Valkey Stream キー名 |
| `VALKEY_STREAM_MAXLEN` | `10000` | Stream の最大保持メッセージ数 |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://tempo:4317` | OpenTelemetry OTLP エンドポイント (gRPC) |
| `OTEL_SERVICE_NAME` | `shake-detection-producer` | OpenTelemetry サービス名 |

> **Note**: .NET の OTLP エクスポーターはデフォルトで gRPC を使用します。Kubernetes 環境では `http://alloy.monitoring.svc.cluster.local:4317` を指定してください。

## OpenTelemetry トレース

以下のスパンが計装されています：

| スパン名 | 説明 |
|----------|------|
| `kyoshin_monitor.process` | タイマーイベント処理全体 |
| `kyoshin_monitor.fetch_image` | 強震モニタ画像の取得 |
| `kyoshin_monitor.decode_image` | 画像のデコード処理 |
| `kyoshin_monitor.analyze_image` | 揺れ検知解析処理 |
| `shake_detected.send` | 揺れ検知イベントの送信判定 |
| `valkey.produce.shake_detected` | Valkey Stream への揺れ検知イベント送信 |
| `valkey.produce.error` | Valkey Stream へのエラーイベント送信 |

## サービス構成

`compose.yaml` には以下のサービスが含まれています：

- **shake-detection-producer**: 揺れ検知プロデューサー本体
- **valkey**: Valkey (Redis互換インメモリデータストア)

## 観測点データについて

観測点データは [ingen084/kyoshin-monitor-observation-points](https://github.com/ingen084/kyoshin-monitor-observation-points) から取得しています。

Docker イメージビルド時に自動的にダウンロードされ、イメージに含まれます。
別のバージョンを使用する場合は、ビルド時に `OBSERVATION_POINTS_TAG` を指定してください。

## Valkey Stream について

揺れ検知イベントは Valkey Stream (Redis Streams 互換) を使用して配信されます。

### コンシューマーグループ

コンシューマーグループを使用することで、複数のコンシューマーで負荷分散や at-least-once 配信保証が可能です。

```bash
# コンシューマーグループの作成
valkey-cli XGROUP CREATE shake-detect-events my-group $ MKSTREAM

# グループでの読み取り
valkey-cli XREADGROUP GROUP my-group consumer1 COUNT 10 STREAMS shake-detect-events >
```

### Stream の確認

```bash
# 最新のメッセージを確認
valkey-cli XREVRANGE shake-detect-events + - COUNT 5

# Stream の情報を取得
valkey-cli XINFO STREAM shake-detect-events
```
