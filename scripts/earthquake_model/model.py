"""
地震震源・規模推定モデル

Phase 1: ベースラインモデル（CNN + Global Pooling）
Phase 2: Transformerモデル（Spatio-Temporal Transformer）
"""

import math

import torch
import torch.nn as nn
import torch.nn.functional as F


class PositionalEncoding(nn.Module):
    """位置エンコーディング（座標ベース）"""

    def __init__(self, d_model: int, max_len: int = 5000):
        super().__init__()
        self.d_model = d_model

        # 標準的なPositional Encoding
        pe = torch.zeros(max_len, d_model)
        position = torch.arange(0, max_len, dtype=torch.float).unsqueeze(1)
        div_term = torch.exp(
            torch.arange(0, d_model, 2).float() * (-math.log(10000.0) / d_model)
        )
        pe[:, 0::2] = torch.sin(position * div_term)
        pe[:, 1::2] = torch.cos(position * div_term)
        self.register_buffer("pe", pe)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        """
        Args:
            x: (batch, seq_len, d_model)
        """
        return x + self.pe[: x.size(1)]


class CoordinateEncoding(nn.Module):
    """
    観測点の座標を特徴量に変換する

    緯度・経度を正規化してMLPで埋め込む
    """

    def __init__(self, d_model: int):
        super().__init__()
        self.mlp = nn.Sequential(
            nn.Linear(2, d_model // 2),
            nn.ReLU(),
            nn.Linear(d_model // 2, d_model),
        )
        # 日本の座標範囲で正規化（緯度: 24-46, 経度: 122-154）
        self.lat_min, self.lat_max = 24.0, 46.0
        self.lon_min, self.lon_max = 122.0, 154.0

    def forward(self, coords: torch.Tensor) -> torch.Tensor:
        """
        Args:
            coords: (batch, num_stations, 2) - [lat, lon]

        Returns:
            (batch, num_stations, d_model)
        """
        # 正規化
        normalized = coords.clone()
        normalized[..., 0] = (coords[..., 0] - self.lat_min) / (self.lat_max - self.lat_min)
        normalized[..., 1] = (coords[..., 1] - self.lon_min) / (self.lon_max - self.lon_min)
        normalized = normalized * 2 - 1  # [-1, 1]に変換

        return self.mlp(normalized)


class BaselineModel(nn.Module):
    """
    ベースラインモデル: CNN + Global Pooling

    シンプルな1D CNNで時間方向の特徴を抽出し、
    観測点方向にPoolingして全結合層で出力する。

    Input: (batch, time, stations)
    Output: {
        "has_earthquake": (batch, 1) - 存在確率,
        "location": (batch, 3) - 緯度, 経度, 深さ,
        "magnitude": (batch, 1)
    }
    """

    # 日本の座標範囲（出力のスケーリング用）
    LAT_MIN, LAT_MAX = 24.0, 46.0
    LON_MIN, LON_MAX = 122.0, 154.0
    DEPTH_MAX = 700.0  # 最大深さ（km）
    MAG_MIN, MAG_MAX = 0.0, 9.0  # マグニチュード範囲

    def __init__(
        self,
        hidden_dim: int = 256,
        num_layers: int = 4,
        dropout: float = 0.1,
    ):
        super().__init__()

        self.hidden_dim = hidden_dim

        # 時間方向の1D CNN
        self.time_conv = nn.Sequential(
            nn.Conv1d(1, 64, kernel_size=5, padding=2),
            nn.BatchNorm1d(64),
            nn.ReLU(),
            nn.Conv1d(64, 128, kernel_size=5, padding=2),
            nn.BatchNorm1d(128),
            nn.ReLU(),
            nn.Conv1d(128, hidden_dim, kernel_size=5, padding=2),
            nn.BatchNorm1d(hidden_dim),
            nn.ReLU(),
        )

        # 座標エンコーディング
        self.coord_encoding = CoordinateEncoding(hidden_dim)

        # 観測点間の情報統合
        self.station_mlp = nn.Sequential(
            nn.Linear(hidden_dim * 2, hidden_dim),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim, hidden_dim),
        )

        # 出力ヘッド
        self.classifier = nn.Sequential(
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 1),
        )

        # 位置出力（正規化された値を出力）
        self.location_head = nn.Sequential(
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 3),  # lat, lon, depth (normalized)
        )

        self.magnitude_head = nn.Sequential(
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 1),
        )

    def forward(
        self,
        intensity: torch.Tensor,
        coords: torch.Tensor,
        mask: torch.Tensor,
    ) -> dict[str, torch.Tensor]:
        """
        Args:
            intensity: (batch, time, stations) - 震度時系列
            coords: (batch, stations, 2) - 観測点座標
            mask: (batch, stations) - 有効な観測点マスク

        Returns:
            dict with keys: has_earthquake, location, magnitude
        """
        batch_size, time_len, num_stations = intensity.shape

        # 各観測点の時系列をCNNで処理
        # (batch, time, stations) -> (batch * stations, 1, time)
        x = intensity.permute(0, 2, 1).reshape(batch_size * num_stations, 1, time_len)
        x = self.time_conv(x)  # (batch * stations, hidden_dim, time)

        # 時間方向をPooling
        x = F.adaptive_avg_pool1d(x, 1).squeeze(-1)  # (batch * stations, hidden_dim)
        x = x.view(batch_size, num_stations, self.hidden_dim)  # (batch, stations, hidden_dim)

        # 座標特徴を追加
        coord_feat = self.coord_encoding(coords)  # (batch, stations, hidden_dim)
        x = torch.cat([x, coord_feat], dim=-1)  # (batch, stations, hidden_dim * 2)
        x = self.station_mlp(x)  # (batch, stations, hidden_dim)

        # マスクを適用して無効な観測点をゼロに
        mask_expanded = mask.unsqueeze(-1).float()  # (batch, stations, 1)
        x = x * mask_expanded

        # 観測点方向をPooling（有効な観測点のみで平均）
        mask_sum = mask_expanded.sum(dim=1, keepdim=True).clamp(min=1)
        x = (x.sum(dim=1) / mask_sum.squeeze(1))  # (batch, hidden_dim)

        # 出力
        has_earthquake = torch.sigmoid(self.classifier(x))  # (batch, 1)

        # 位置（sigmoidで[0,1]に正規化してからスケーリング）
        location_raw = torch.sigmoid(self.location_head(x))  # (batch, 3) in [0, 1]
        lat = location_raw[:, 0:1] * (self.LAT_MAX - self.LAT_MIN) + self.LAT_MIN
        lon = location_raw[:, 1:2] * (self.LON_MAX - self.LON_MIN) + self.LON_MIN
        depth = location_raw[:, 2:3] * self.DEPTH_MAX
        location = torch.cat([lat, lon, depth], dim=1)  # (batch, 3)

        # マグニチュード（sigmoidで[0,1]にしてスケーリング）
        magnitude_raw = torch.sigmoid(self.magnitude_head(x))  # (batch, 1) in [0, 1]
        magnitude = magnitude_raw * (self.MAG_MAX - self.MAG_MIN) + self.MAG_MIN

        return {
            "has_earthquake": has_earthquake,
            "location": location,
            "magnitude": magnitude,
        }


class EarthquakeTransformer(nn.Module):
    """
    Spatio-Temporal Transformer

    各観測点をトークンとして扱い、Self-Attentionで
    観測点間の関係を学習する。

    Input: (batch, time, stations)
    Output: {
        "has_earthquake": (batch, 1) - 存在確率,
        "location": (batch, 3) - 緯度, 経度, 深さ,
        "magnitude": (batch, 1)
    }
    """

    # 日本の座標範囲（出力のスケーリング用）
    LAT_MIN, LAT_MAX = 24.0, 46.0
    LON_MIN, LON_MAX = 122.0, 154.0
    DEPTH_MAX = 700.0
    MAG_MIN, MAG_MAX = 0.0, 9.0

    def __init__(
        self,
        hidden_dim: int = 256,
        num_layers: int = 4,
        num_heads: int = 8,
        dropout: float = 0.1,
        max_stations: int = 2000,
    ):
        super().__init__()

        self.hidden_dim = hidden_dim

        # 時間方向の特徴抽出（1D CNN）
        self.time_encoder = nn.Sequential(
            nn.Conv1d(1, 64, kernel_size=5, padding=2),
            nn.BatchNorm1d(64),
            nn.ReLU(),
            nn.Conv1d(64, 128, kernel_size=5, padding=2),
            nn.BatchNorm1d(128),
            nn.ReLU(),
            nn.AdaptiveAvgPool1d(16),  # 時間方向を16に圧縮
            nn.Flatten(),
            nn.Linear(128 * 16, hidden_dim),
        )

        # 座標エンコーディング
        self.coord_encoding = CoordinateEncoding(hidden_dim)

        # CLSトークン
        self.cls_token = nn.Parameter(torch.randn(1, 1, hidden_dim))

        # Transformer Encoder
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=hidden_dim,
            nhead=num_heads,
            dim_feedforward=hidden_dim * 4,
            dropout=dropout,
            activation="gelu",
            batch_first=True,
        )
        self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=num_layers)

        # 出力ヘッド
        self.classifier = nn.Sequential(
            nn.LayerNorm(hidden_dim),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.GELU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 1),
        )

        self.location_head = nn.Sequential(
            nn.LayerNorm(hidden_dim),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.GELU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 3),
        )

        self.magnitude_head = nn.Sequential(
            nn.LayerNorm(hidden_dim),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.GELU(),
            nn.Dropout(dropout),
            nn.Linear(hidden_dim // 2, 1),
        )

    def forward(
        self,
        intensity: torch.Tensor,
        coords: torch.Tensor,
        mask: torch.Tensor,
    ) -> dict[str, torch.Tensor]:
        """
        Args:
            intensity: (batch, time, stations) - 震度時系列
            coords: (batch, stations, 2) - 観測点座標
            mask: (batch, stations) - 有効な観測点マスク

        Returns:
            dict with keys: has_earthquake, location, magnitude
        """
        batch_size, time_len, num_stations = intensity.shape

        # 各観測点の時系列を特徴ベクトルに変換
        # (batch, time, stations) -> (batch * stations, 1, time)
        x = intensity.permute(0, 2, 1).reshape(batch_size * num_stations, 1, time_len)
        x = self.time_encoder(x)  # (batch * stations, hidden_dim)
        x = x.view(batch_size, num_stations, self.hidden_dim)  # (batch, stations, hidden_dim)

        # 座標特徴を加算
        coord_feat = self.coord_encoding(coords)  # (batch, stations, hidden_dim)
        x = x + coord_feat

        # CLSトークンを追加
        cls_tokens = self.cls_token.expand(batch_size, -1, -1)  # (batch, 1, hidden_dim)
        x = torch.cat([cls_tokens, x], dim=1)  # (batch, 1 + stations, hidden_dim)

        # マスクを作成（CLSトークンは常に有効）
        cls_mask = torch.ones(batch_size, 1, dtype=torch.bool, device=mask.device)
        full_mask = torch.cat([cls_mask, mask], dim=1)  # (batch, 1 + stations)

        # Transformerに通す（無効なトークンをマスク）
        # src_key_padding_mask: Trueの位置が無視される
        padding_mask = ~full_mask
        x = self.transformer(x, src_key_padding_mask=padding_mask)

        # CLSトークンの出力を使用
        cls_output = x[:, 0, :]  # (batch, hidden_dim)

        # 出力
        has_earthquake = torch.sigmoid(self.classifier(cls_output))

        # 位置（sigmoidで[0,1]に正規化してからスケーリング）
        location_raw = torch.sigmoid(self.location_head(cls_output))  # (batch, 3) in [0, 1]
        lat = location_raw[:, 0:1] * (self.LAT_MAX - self.LAT_MIN) + self.LAT_MIN
        lon = location_raw[:, 1:2] * (self.LON_MAX - self.LON_MIN) + self.LON_MIN
        depth = location_raw[:, 2:3] * self.DEPTH_MAX
        location = torch.cat([lat, lon, depth], dim=1)

        # マグニチュード
        magnitude_raw = torch.sigmoid(self.magnitude_head(cls_output))
        magnitude = magnitude_raw * (self.MAG_MAX - self.MAG_MIN) + self.MAG_MIN

        return {
            "has_earthquake": has_earthquake,
            "location": location,
            "magnitude": magnitude,
        }


def create_model(
    model_type: str = "baseline",
    hidden_dim: int = 256,
    num_layers: int = 4,
    num_heads: int = 8,
    dropout: float = 0.1,
) -> nn.Module:
    """
    モデルを作成する

    Args:
        model_type: "baseline" or "transformer"
        hidden_dim: 隠れ層の次元数
        num_layers: レイヤー数
        num_heads: Attentionヘッド数（Transformerのみ）
        dropout: ドロップアウト率

    Returns:
        モデル
    """
    if model_type == "baseline":
        return BaselineModel(
            hidden_dim=hidden_dim,
            num_layers=num_layers,
            dropout=dropout,
        )
    elif model_type == "transformer":
        return EarthquakeTransformer(
            hidden_dim=hidden_dim,
            num_layers=num_layers,
            num_heads=num_heads,
            dropout=dropout,
        )
    else:
        raise ValueError(f"Unknown model type: {model_type}")
