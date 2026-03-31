# HypocenterSearchEngine 仕様書

## 概要

`HypocenterSearchEngine` は、揺れ検知データから震源要素（震央位置、深さ、発震時刻）を推定するエンジンです。グリッドサーチと Nelder-Mead 法（シンプレックス法）による2段階最適化アルゴリズムを使用し、高速かつ高精度な震源推定を実現します。

### 名前空間

```csharp
namespace KyoshinEewViewer.TravelTimeTable;
```

### 依存関係

- `TravelTimeCalculator`: P波・S波の走時計算
- `TravelTimeTable`: 走時表データ
- `KyoshinMonitorLib.Location`: 位置情報

---

## アーキテクチャ

```
┌──────────────────────────────────────────────────────────────────┐
│                    HypocenterSearchEngine                        │
├──────────────────────────────────────────────────────────────────┤
│ 入力: DetectionPoint[] (検知観測点)                              │
│       UndetectedStation[]? (未検知観測点、オプション)            │
│       DateTime? (現在時刻、オプション)                           │
├──────────────────────────────────────────────────────────────────┤
│           ┌────────────────────────────────┐                     │
│           │  Phase 1: グリッドサーチ       │                     │
│           │  - 粗い探索（0.1度グリッド）   │                     │
│           │  - 並列処理で高速化            │                     │
│           │  - 観測点サンプリング          │                     │
│           └─────────────┬──────────────────┘                     │
│                         ▼                                        │
│           ┌────────────────────────────────┐                     │
│           │  Phase 2: Nelder-Mead 精密化   │                     │
│           │  - シンプレックス法            │                     │
│           │  - 収束まで反復               │                     │
│           └─────────────┬──────────────────┘                     │
│                         ▼                                        │
│ 出力: EstimatedHypocenter (推定震源要素)                         │
└──────────────────────────────────────────────────────────────────┘
```

---

## データモデル

### DetectionPoint（検知観測点）

揺れを検知した観測点の情報を保持します。

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Location` | `Location` | 観測点の位置（緯度・経度） |
| `DetectedAt` | `DateTime` | 揺れ検知時刻 |
| `Code` | `string?` | 観測点コード（オプション） |
| `Intensity` | `double?` | 検知時の震度（オプション） |

### UndetectedStation（未検知観測点）

まだ揺れを検知していない観測点の情報を保持します。

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Location` | `Location` | 観測点の位置（緯度・経度） |
| `Code` | `string?` | 観測点コード（オプション） |

### EstimatedHypocenter（推定震源）

推定された震源情報を保持します。

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Location` | `Location` | 推定震央位置（緯度・経度） |
| `DepthKm` | `int` | 推定震源深さ (km) |
| `OriginTime` | `DateTime` | 推定発震時刻 |
| `ConfidenceScore` | `double` | 信頼度スコア（0.0〜1.0） |
| `UsedStationCount` | `int` | 推定に使用した観測点数 |
| `ResidualStdDev` | `double` | 残差の標準偏差（秒） |
| `CalculationTimeMs` | `double` | 総計算時間（ミリ秒） |
| `GridSearchTimeMs` | `double` | グリッドサーチ時間（ミリ秒） |
| `RefinementTimeMs` | `double` | Nelder-Mead精密化時間（ミリ秒） |
| `AlgorithmVersion` | `int` | アルゴリズムバージョン（デフォルト: 1） |
| `UpdatedAt` | `DateTime` | 最終更新時刻 |

---

## アルゴリズム詳細

### Phase 1: グリッドサーチ

#### 概要

検知観測点の重心を中心に、指定された範囲をグリッド状に探索し、最も残差スコアが低い候補を特定します。

#### 処理フロー

1. **探索中心の決定**: 検知観測点の緯度・経度の平均値を探索中心とする
2. **観測点のサンプリング**: 計算時間短縮のため、最大50点をサンプリング
   - 前半: 時刻順で初期検知点を優先選択
   - 後半: 空間的に分散した観測点を選択（既選択点から最も離れた点を順次選択）
3. **グリッド生成**: 探索範囲内の全グリッドポイント（緯度、経度、深さの組み合わせ）を生成
4. **並列探索**: 各グリッドポイントに対して並列に以下を計算
   - 発震時刻の推定
   - 残差スコアの計算
   - 大残差ペナルティの計算
   - 未検知ペナルティの計算（条件付き）
5. **最良候補の選択**: スコアが最小のグリッドポイントを選択

#### 発震時刻推定

各検知観測点について、P波またはS波の走時を逆算して発震時刻を推定し、**中央値**を採用します。S波による推定を優先します（一般的にS波で検知することが多いため）。

### Phase 2: Nelder-Mead法（シンプレックス法）

#### 概要

グリッドサーチで得られた粗い解を初期値として、Nelder-Mead法で高精度に精密化します。

#### 処理フロー

1. **初期シンプレックス生成**: 4頂点（4次元: 緯度、経度、深さ、発震時刻）
   - 頂点0: グリッドサーチの最良解
   - 頂点1: 緯度 + 0.1度
   - 頂点2: 経度 + 0.1度
   - 頂点3: 深さ + 10km
2. **反復最適化**: 収束条件を満たすか最大反復回数に達するまで以下を繰り返し
   - **反射** (Reflection): 最悪点を重心の反対側に反射
   - **拡大** (Expansion): 反射点が最良の場合、さらに拡大
   - **収縮** (Contraction): 反射点が悪い場合、収縮
   - **縮小** (Shrink): 収縮でも改善しない場合、全体を縮小
3. **結果の丸め込み**
   - 緯度・経度: 0.1度単位
   - 深さ: 10km単位

#### Nelder-Mead係数

| 係数 | デフォルト値 | 説明 |
|------|-------------|------|
| 反射係数 | 1.0 | 反射の倍率 |
| 拡大係数 | 2.0 | 拡大の倍率 |
| 収縮係数 | 0.5 | 収縮の倍率 |
| 縮小係数 | 0.5 | 縮小の倍率 |

---

## スコア計算

### 残差スコア

理論到達時刻と観測時刻の差分（残差）の二乗和を計算します。

```
Score = Σ(residual_i)²
```

各観測点について、P波残差とS波残差の両方を計算し、**絶対値が小さい方**を採用します。

### 大残差ペナルティ

残差が閾値（デフォルト: 3秒）を超える観測点に対して、追加のペナルティを加算します。

```
LargeResidualPenalty = Σ (|residual| - threshold) × factor
                       (|residual| > threshold の観測点のみ)
```

### 未検知ペナルティ

理論上は揺れが到達済みのはずなのに未検知の観測点がある場合、ペナルティを加算します。

| 条件 | ペナルティ倍率 |
|------|---------------|
| P波のみ到達済み | 1.0倍 |
| S波到達済み | 2.0倍（S波の方が振幅が大きいため） |

#### 適用条件

未検知ペナルティは以下の条件で適用されます:

- 揺れ検知から3秒以内、または
- 揺れ検知から10秒以内かつ検知数30点未満

#### 対象範囲

最大震央距離 + 30km 以内の未検知観測点のみを対象とします。

---

## 信頼度スコア

残差の標準偏差から指数関数的に信頼度を算出します。

```
ConfidenceScore = exp(-ResidualStdDev / ScaleFactor)
```

残差が小さいほど信頼度が高くなります（最大1.0、最小0.0）。

---

## パラメータ設定

### HypocenterSearchParameters

| パラメータ | 型 | デフォルト値 | 説明 |
|-----------|-----|-------------|------|
| `MinStationCount` | `int` | 3 | 探索に必要な最小観測点数 |
| `GridSearchRangeDeg` | `double` | 2.0 | グリッドサーチの探索範囲（度） |
| `GridSearchStepDeg` | `double` | 0.1 | グリッドサーチのステップ（度） |
| `MinDepthKm` | `int` | 0 | 最小深さ (km) |
| `MaxDepthKm` | `int` | 700 | 最大深さ (km) |
| `DepthStepKm` | `int` | 10 | 深さのステップ (km) |
| `MaxGridSearchStations` | `int` | 50 | グリッドサーチで使用する最大観測点数 |
| `MaxIterations` | `int` | 100 | Nelder-Mead法の最大反復回数 |
| `ConvergenceThreshold` | `double` | 0.01 | 収束判定閾値 |
| `SimplexInitialSizeDeg` | `double` | 0.1 | 初期シンプレックスのサイズ（度） |
| `SimplexInitialSizeDepth` | `double` | 10 | 初期シンプレックスのサイズ（深さ km） |
| `ReflectionCoef` | `double` | 1.0 | 反射係数 |
| `ExpansionCoef` | `double` | 2.0 | 拡大係数 |
| `ContractionCoef` | `double` | 0.5 | 収縮係数 |
| `ShrinkCoef` | `double` | 0.5 | 縮小係数 |
| `ConfidenceScaleFactor` | `double` | 5.0 | 信頼度計算のスケールファクター |
| `UndetectedPenaltyFactor` | `double` | 5.0 | 未検知ペナルティ係数 |
| `LargeResidualThresholdSeconds` | `double` | 3.0 | 大残差と判定する閾値（秒） |
| `LargeResidualPenaltyFactor` | `double` | 2.0 | 大残差ペナルティ係数 |

---

## 公開API

### コンストラクタ

```csharp
// TravelTimeCalculatorを直接指定
public HypocenterSearchEngine(TravelTimeCalculator calculator)

// TravelTimeTableから生成
public HypocenterSearchEngine(TravelTimeTable travelTimeTable)
```

### メソッド

#### Search（基本版）

```csharp
public EstimatedHypocenter? Search(IReadOnlyList<DetectionPoint> detections)
```

検知点のみから震源要素を推定します。

**パラメータ:**

- `detections`: 検知観測点のリスト

**戻り値:**

- `EstimatedHypocenter`: 推定震源要素
- `null`: 推定できない場合（観測点数不足など）

#### Search（未検知ペナルティ考慮版）

```csharp
public EstimatedHypocenter? Search(
    IReadOnlyList<DetectionPoint> detections,
    IReadOnlyList<UndetectedStation>? undetectedStations,
    DateTime? currentTime)
```

未検知観測点のペナルティを考慮して震源要素を推定します。

**パラメータ:**

- `detections`: 検知観測点のリスト
- `undetectedStations`: 未検知観測点のリスト（null可）
- `currentTime`: 現在時刻（null可）

**戻り値:**

- `EstimatedHypocenter`: 推定震源要素
- `null`: 推定できない場合

#### IsConsistent

```csharp
public bool IsConsistent(DetectionPoint detection, EstimatedHypocenter hypocenter, double toleranceSeconds)
```

検知点が指定された震源要素と整合するかを判定します。同一イベント判定に使用します。

**パラメータ:**

- `detection`: 検知観測点
- `hypocenter`: 震源要素
- `toleranceSeconds`: 許容誤差（秒）

**戻り値:**

- `true`: P波またはS波のいずれかで許容誤差内
- `false`: 整合しない

#### CalculateConsistencyRatio

```csharp
public double CalculateConsistencyRatio(
    IReadOnlyList<DetectionPoint> detections,
    EstimatedHypocenter hypocenter,
    double toleranceSeconds)
```

複数の検知点が同一イベントに属するかの割合を計算します。

**パラメータ:**

- `detections`: 検知観測点のリスト
- `hypocenter`: 震源要素
- `toleranceSeconds`: 許容誤差（秒）

**戻り値:**

- 整合する検知点の割合（0.0〜1.0）

---

## 使用例

### 基本的な使用

```csharp
// TravelTimeTableを読み込み
var travelTimeTable = TravelTimeTable.Load("travel_time.dat");

// エンジンを初期化
var engine = new HypocenterSearchEngine(travelTimeTable);

// 検知データを準備
var detections = new List<DetectionPoint>
{
    new(new Location(35.0f, 139.0f), DateTime.Now.AddSeconds(-5), "STATION_A"),
    new(new Location(35.1f, 139.1f), DateTime.Now.AddSeconds(-4), "STATION_B"),
    new(new Location(35.2f, 139.0f), DateTime.Now.AddSeconds(-3), "STATION_C"),
};

// 震源を探索
var result = engine.Search(detections);
if (result != null)
{
    Console.WriteLine($"震央: ({result.Location.Latitude}, {result.Location.Longitude})");
    Console.WriteLine($"深さ: {result.DepthKm} km");
    Console.WriteLine($"発震時刻: {result.OriginTime}");
    Console.WriteLine($"信頼度: {result.ConfidenceScore:P1}");
}
```

### 未検知ペナルティを考慮した使用

```csharp
// 未検知観測点リスト
var undetectedStations = new List<UndetectedStation>
{
    new(new Location(35.05f, 139.05f), "STATION_D"),
    new(new Location(35.15f, 139.15f), "STATION_E"),
};

// 現在時刻を指定して探索
var result = engine.Search(detections, undetectedStations, DateTime.Now);
```

### パラメータのカスタマイズ

```csharp
var engine = new HypocenterSearchEngine(travelTimeTable)
{
    Parameters = new HypocenterSearchParameters
    {
        MinStationCount = 5,           // 最小5点必要
        GridSearchRangeDeg = 3.0,      // 探索範囲を広く
        MaxIterations = 200,           // より精密に
        UndetectedPenaltyFactor = 10.0 // 未検知ペナルティを強化
    }
};
```

---

## パフォーマンス特性

### 計算量

- **グリッドサーチ**: O(N × G)
  - N: サンプリング後の観測点数（最大50）
  - G: グリッドポイント数（デフォルト: 41 × 41 × 71 ≒ 119,311）
- **Nelder-Mead**: O(I × N)
  - I: 反復回数（最大100）
  - N: 観測点数

### 並列化

グリッドサーチは `Parallel.ForEach` により並列実行されます。

### 典型的な実行時間

| 観測点数 | グリッドサーチ | Nelder-Mead | 合計 |
|---------|--------------|-------------|------|
| 10点 | 〜50ms | 〜5ms | 〜55ms |
| 50点 | 〜100ms | 〜10ms | 〜110ms |
| 100点 | 〜150ms | 〜15ms | 〜165ms |

※ 実行環境により大きく異なります

---

## 制限事項

1. **観測点数**: 最低3点以上必要
2. **深さ範囲**: 0〜700km（走時表の制限による）
3. **精度**:
   - 緯度・経度: 0.1度単位で丸め込み
   - 深さ: 10km単位で丸め込み
4. **走時表依存**: 走時表の精度が推定精度に直接影響

---

## 関連クラス

- `TravelTimeCalculator`: P波・S波の走時計算
- `TravelTimeTable`: 走時表データの管理
- `Location` (KyoshinMonitorLib): 位置情報

---

## 変更履歴

| バージョン | 日付 | 変更内容 |
|-----------|------|---------|
| 1.0 | - | 初期実装 |
