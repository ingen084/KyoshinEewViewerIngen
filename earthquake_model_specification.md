# 地震震源・規模推定モデル 実装指示書

## プロジェクト概要

強震モニタの計測震度データ（約1500観測点、1秒間隔）から、地震の震源位置（緯度・経度・深さ）とマグニチュードを推定するモデルを構築する。

### 特徴
- 入力は波形ではなく計測震度（スカラー値）
- 観測点数は時期により増減する（可変長対応が必要）
- 将来的に複数地震の同時検出にも対応予定

---

## 技術スタック

- **フレームワーク**: PyTorch
- **Python**: 3.9以上推奨

---

## データ仕様

### 入力データファイル形式（npz）

各ファイルは1つの大地震イベント＋余震群、または地震なし期間を含む。

```python
{
    "intensity": np.ndarray,       # shape: (total_time, max_stations)
                                   # 計測震度（小数点付き、例: 3.2）
                                   # 欠損・無効な観測点は np.nan
    
    "coords": np.ndarray,          # shape: (max_stations, 2)
                                   # 各観測点の [緯度, 経度]
    
    "station_mask": np.ndarray,    # shape: (max_stations,)
                                   # 有効な観測点: True, 無効: False
    
    "earthquakes": list[dict]      # この期間内の地震リスト
    # 各要素:
    # {
    #     "time_index": int,       # 発生時刻のインデックス（秒）
    #     "latitude": float,       # 震源緯度
    #     "longitude": float,      # 震源経度
    #     "depth": float,          # 震源深さ (km)
    #     "magnitude": float       # マグニチュード
    # }
}
```

### ディレクトリ構造

```
data/
├── raw/                    # パース済みの生データ
├── processed/              # npz形式に変換後
│   ├── event_001.npz
│   ├── event_002.npz
│   └── background_001.npz  # 地震なし期間
└── metadata/               # 観測点情報など
```

---

## 実装コンポーネント

### 1. データ前処理スクリプト (`preprocess.py`)

パース済みデータをnpz形式に変換する。

**入力**: 既に作成したパーサーの出力（形式は要確認）
**出力**: 上記npz形式

### 2. Datasetクラス (`dataset.py`)

```python
class EarthquakeDataset(torch.utils.data.Dataset):
    """
    Parameters:
        file_paths: list[str] - npzファイルのパスリスト
        window_size: int - 時間窓の長さ（デフォルト: 120秒）
        include_no_earthquake: bool - 地震なしサンプルを含めるか
    
    Returns (__getitem__):
        {
            "intensity": Tensor (window_size, num_stations),
            "coords": Tensor (num_stations, 2),
            "mask": Tensor (num_stations,) - 有効な観測点のブールマスク,
            "label": {
                "has_earthquake": bool,
                "latitude": float or None,
                "longitude": float or None,
                "depth": float or None,
                "magnitude": float or None
            }
        }
    """
```

**実装要件**:
- 連続データからランダムに時間窓を切り出す
- 地震発生タイミングを含む窓と含まない窓の両方を生成
- 地震ありの場合、発生5-10秒前から切り出し開始を推奨

### 3. Collate関数 (`dataset.py`)

観測点数が異なるサンプルをバッチ化するためのcollate関数。

```python
def collate_fn(batch):
    """
    可変長の観測点をパディングしてバッチ化
    
    Returns:
        {
            "intensity": Tensor (batch, window_size, max_stations_in_batch),
            "coords": Tensor (batch, max_stations_in_batch, 2),
            "mask": Tensor (batch, max_stations_in_batch),
            "labels": バッチ化されたラベル
        }
    """
```

### 4. モデル (`model.py`)

#### Phase 1: ベースラインモデル（まず動作確認用）

```python
class BaselineModel(nn.Module):
    """
    シンプルなCNN + Global Poolingモデル
    - 時間方向に1D CNNで特徴抽出
    - 観測点方向にPooling
    - 全結合層で出力
    
    Input: (batch, time, stations)
    Output: {
        "has_earthquake": (batch, 1) - 存在確率,
        "location": (batch, 3) - 緯度, 経度, 深さ,
        "magnitude": (batch, 1)
    }
    """
```

#### Phase 2: Transformerモデル（本命）

```python
class EarthquakeTransformer(nn.Module):
    """
    Spatio-Temporal Transformer
    
    構成:
    1. 入力埋め込み
       - 震度時系列 → 時間方向のCNN or Linear
       - 観測点座標 → 位置エンコーディングとして使用
    
    2. Transformer Encoder
       - 各観測点をトークンとして扱う
       - Self-Attentionで観測点間の関係を学習
    
    3. 出力ヘッド
       - [CLS]トークン or Global Pooling → 予測
    
    注意点:
    - 観測点の座標を位置エンコーディングに組み込む
    - マスクされた観測点はAttentionから除外
    """
```

### 5. 損失関数 (`loss.py`)

```python
class EarthquakeLoss(nn.Module):
    """
    マルチタスク損失
    
    - has_earthquake: Binary Cross Entropy
    - location: MSE or Haversine距離ベースの損失
    - magnitude: MSE
    
    地震なしサンプルではlocation/magnitudeの損失は計算しない
    """
```

### 6. 学習スクリプト (`train.py`)

```python
"""
- DataLoaderの設定（collate_fn使用）
- 学習ループ
- バリデーション
- チェックポイント保存
- TensorBoard or wandbでのログ
"""
```

### 7. 評価スクリプト (`evaluate.py`)

```python
"""
評価指標:
- 地震検出: Precision, Recall, F1
- 震源位置: 平均距離誤差 (km)
- 深さ: 平均絶対誤差 (km)
- マグニチュード: 平均絶対誤差
"""
```

---

## 設定ファイル (`config.yaml`)

```yaml
data:
  window_size: 120
  train_ratio: 0.8
  
model:
  type: "baseline"  # or "transformer"
  hidden_dim: 256
  num_layers: 4
  num_heads: 8
  dropout: 0.1

training:
  batch_size: 32
  learning_rate: 0.001
  epochs: 100
  early_stopping_patience: 10

output:
  checkpoint_dir: "checkpoints/"
  log_dir: "logs/"
```

---

## 実装の優先順位

1. **Dataset + Collate関数** - データが流れることを確認
2. **BaselineModel** - 最小構成で学習が回ることを確認
3. **損失関数 + 学習ループ** - 実際に学習
4. **評価スクリプト** - 精度確認
5. **Transformerモデル** - 本格的なモデルに移行

---

## 注意事項

- 観測点数は可変（時期により1300〜1500程度）
- 古いデータには現在存在しない観測点が含まれる
- 計測震度の欠損値は `np.nan` で表現
- 将来的に複数地震の同時検出に拡張予定（現時点では単一地震のみ）

---

## 拡張予定（Phase 2以降）

- 複数地震の同時検出（DETR的アプローチ）
- リアルタイム推論対応
- 不確実性の推定（震源位置の信頼区間など）
