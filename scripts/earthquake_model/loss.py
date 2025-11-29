"""
損失関数

マルチタスク損失:
- has_earthquake: Binary Cross Entropy
- location: MSE または Haversine距離ベースの損失
- magnitude: MSE

地震なしサンプルではlocation/magnitudeの損失は計算しない
"""

import math

import torch
import torch.nn as nn
import torch.nn.functional as F


def haversine_distance(
    lat1: torch.Tensor,
    lon1: torch.Tensor,
    lat2: torch.Tensor,
    lon2: torch.Tensor,
) -> torch.Tensor:
    """
    Haversine公式で2点間の距離を計算する（km）

    Args:
        lat1, lon1: 点1の緯度・経度（度）
        lat2, lon2: 点2の緯度・経度（度）

    Returns:
        距離（km）
    """
    R = 6371.0  # 地球の半径（km）

    # 度をラジアンに変換
    lat1_rad = torch.deg2rad(lat1)
    lat2_rad = torch.deg2rad(lat2)
    dlat = torch.deg2rad(lat2 - lat1)
    dlon = torch.deg2rad(lon2 - lon1)

    # Haversine公式
    a = torch.sin(dlat / 2) ** 2 + torch.cos(lat1_rad) * torch.cos(lat2_rad) * torch.sin(dlon / 2) ** 2
    c = 2 * torch.atan2(torch.sqrt(a), torch.sqrt(1 - a))

    return R * c


class EarthquakeLoss(nn.Module):
    """
    地震予測のマルチタスク損失

    Parameters:
        classification_weight: 分類損失の重み
        location_weight: 位置損失の重み
        depth_weight: 深さ損失の重み
        magnitude_weight: マグニチュード損失の重み
        use_haversine: Haversine距離を使用するか（Falseの場合はMSE）
    """

    def __init__(
        self,
        classification_weight: float = 1.0,
        location_weight: float = 0.01,  # km単位なのでスケールダウン
        depth_weight: float = 0.001,    # km単位なのでスケールダウン
        magnitude_weight: float = 1.0,
        use_haversine: bool = True,
    ):
        super().__init__()
        self.classification_weight = classification_weight
        self.location_weight = location_weight
        self.depth_weight = depth_weight
        self.magnitude_weight = magnitude_weight
        self.use_haversine = use_haversine

        self.bce_loss = nn.BCELoss(reduction="mean")
        self.mse_loss = nn.MSELoss(reduction="none")

    def forward(
        self,
        predictions: dict[str, torch.Tensor],
        labels: dict[str, torch.Tensor],
    ) -> dict[str, torch.Tensor]:
        """
        損失を計算する

        Args:
            predictions: モデルの出力
                - has_earthquake: (batch, 1)
                - location: (batch, 3) - [lat, lon, depth]
                - magnitude: (batch, 1)
            labels: ラベル
                - has_earthquake: (batch,)
                - latitude: (batch,)
                - longitude: (batch,)
                - depth: (batch,)
                - magnitude: (batch,)

        Returns:
            損失の辞書（total, classification, location, depth, magnitude）
        """
        batch_size = labels["has_earthquake"].shape[0]
        device = predictions["has_earthquake"].device

        # 分類損失
        pred_cls = predictions["has_earthquake"].squeeze(-1)
        target_cls = labels["has_earthquake"]
        loss_cls = self.bce_loss(pred_cls, target_cls)

        # 地震ありサンプルのマスク
        eq_mask = labels["has_earthquake"] > 0.5  # (batch,)
        num_eq_samples = eq_mask.sum().item()

        # 位置・深さ・マグニチュード損失（地震ありサンプルのみ）
        if num_eq_samples > 0:
            # 予測値
            pred_lat = predictions["location"][:, 0]  # (batch,)
            pred_lon = predictions["location"][:, 1]  # (batch,)
            pred_depth = predictions["location"][:, 2]  # (batch,)
            pred_mag = predictions["magnitude"].squeeze(-1)  # (batch,)

            # 正解値
            target_lat = labels["latitude"]
            target_lon = labels["longitude"]
            target_depth = labels["depth"]
            target_mag = labels["magnitude"]

            # 位置損失
            if self.use_haversine:
                # Haversine距離（km）
                distance = haversine_distance(
                    pred_lat[eq_mask],
                    pred_lon[eq_mask],
                    target_lat[eq_mask],
                    target_lon[eq_mask],
                )
                loss_location = distance.mean()
            else:
                # MSE（緯度・経度）
                lat_loss = self.mse_loss(pred_lat[eq_mask], target_lat[eq_mask])
                lon_loss = self.mse_loss(pred_lon[eq_mask], target_lon[eq_mask])
                loss_location = (lat_loss + lon_loss).mean()

            # 深さ損失（MSE、km単位）
            loss_depth = self.mse_loss(pred_depth[eq_mask], target_depth[eq_mask]).mean()

            # マグニチュード損失（MSE）
            loss_magnitude = self.mse_loss(pred_mag[eq_mask], target_mag[eq_mask]).mean()
        else:
            # 地震なしサンプルのみの場合
            loss_location = torch.tensor(0.0, device=device)
            loss_depth = torch.tensor(0.0, device=device)
            loss_magnitude = torch.tensor(0.0, device=device)

        # 総損失
        total_loss = (
            self.classification_weight * loss_cls
            + self.location_weight * loss_location
            + self.depth_weight * loss_depth
            + self.magnitude_weight * loss_magnitude
        )

        return {
            "total": total_loss,
            "classification": loss_cls,
            "location": loss_location,
            "depth": loss_depth,
            "magnitude": loss_magnitude,
        }


class FocalLoss(nn.Module):
    """
    Focal Loss for imbalanced classification

    FL(p_t) = -alpha_t * (1 - p_t)^gamma * log(p_t)
    """

    def __init__(self, alpha: float = 0.25, gamma: float = 2.0):
        super().__init__()
        self.alpha = alpha
        self.gamma = gamma

    def forward(self, pred: torch.Tensor, target: torch.Tensor) -> torch.Tensor:
        """
        Args:
            pred: (batch,) - 予測確率
            target: (batch,) - 正解ラベル（0 or 1）
        """
        bce_loss = F.binary_cross_entropy(pred, target, reduction="none")
        p_t = pred * target + (1 - pred) * (1 - target)
        alpha_t = self.alpha * target + (1 - self.alpha) * (1 - target)
        focal_weight = alpha_t * (1 - p_t) ** self.gamma

        return (focal_weight * bce_loss).mean()


class EarthquakeFocalLoss(EarthquakeLoss):
    """
    Focal Lossを使用した地震予測損失

    クラス不均衡に対応するためFocal Lossを使用
    """

    def __init__(
        self,
        classification_weight: float = 1.0,
        location_weight: float = 1.0,
        depth_weight: float = 0.1,
        magnitude_weight: float = 1.0,
        use_haversine: bool = True,
        focal_alpha: float = 0.25,
        focal_gamma: float = 2.0,
    ):
        super().__init__(
            classification_weight=classification_weight,
            location_weight=location_weight,
            depth_weight=depth_weight,
            magnitude_weight=magnitude_weight,
            use_haversine=use_haversine,
        )
        self.focal_loss = FocalLoss(alpha=focal_alpha, gamma=focal_gamma)

    def forward(
        self,
        predictions: dict[str, torch.Tensor],
        labels: dict[str, torch.Tensor],
    ) -> dict[str, torch.Tensor]:
        """損失を計算する（分類にFocal Lossを使用）"""
        result = super().forward(predictions, labels)

        # 分類損失をFocal Lossで置き換え
        pred_cls = predictions["has_earthquake"].squeeze(-1)
        target_cls = labels["has_earthquake"]
        loss_cls = self.focal_loss(pred_cls, target_cls)

        result["classification"] = loss_cls
        result["total"] = (
            self.classification_weight * loss_cls
            + self.location_weight * result["location"]
            + self.depth_weight * result["depth"]
            + self.magnitude_weight * result["magnitude"]
        )

        return result


def create_loss(
    loss_type: str = "standard",
    **kwargs,
) -> nn.Module:
    """
    損失関数を作成する

    Args:
        loss_type: "standard" or "focal"
        **kwargs: 損失関数のパラメータ

    Returns:
        損失関数
    """
    if loss_type == "standard":
        return EarthquakeLoss(**kwargs)
    elif loss_type == "focal":
        return EarthquakeFocalLoss(**kwargs)
    else:
        raise ValueError(f"Unknown loss type: {loss_type}")
