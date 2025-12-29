# ShakeDetectionProducer

強震モニタの観測点データを取得し、揺れ検知イベントを Kafka に送信するサービスです。

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
| `KAFKA_BOOTSTRAP_SERVERS` | `kafka:9092` | Kafka ブートストラップサーバー |
| `KAFKA_TOPIC` | `shake-detect-events` | Kafka トピック名 |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://tempo:4317` | OpenTelemetry エクスポーターエンドポイント |
| `OTEL_SERVICE_NAME` | `shake-detection-producer` | OpenTelemetry サービス名 |

## サービス構成

`compose.yaml` には以下のサービスが含まれています：

- **shake-detection-producer**: 揺れ検知プロデューサー本体
- **kafka**: Apache Kafka (KRaft モード)

## 観測点データについて

観測点データは [ingen084/kyoshin-monitor-observation-points](https://github.com/ingen084/kyoshin-monitor-observation-points) から取得しています。

Docker イメージビルド時に自動的にダウンロードされ、イメージに含まれます。
別のバージョンを使用する場合は、ビルド時に `OBSERVATION_POINTS_TAG` を指定してください。
