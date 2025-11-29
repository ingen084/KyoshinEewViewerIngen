#!/usr/bin/env python3
"""
学習スクリプト

使用方法:
    python train.py --config config.yaml
    python train.py --data_dir ./data --epochs 100
"""

import argparse
import json
import os
import sys
from datetime import datetime
from pathlib import Path

import torch
import torch.nn as nn
from torch.optim import AdamW
from torch.optim.lr_scheduler import CosineAnnealingLR, ReduceLROnPlateau
from tqdm import tqdm

try:
    import yaml
    HAS_YAML = True
except ImportError:
    HAS_YAML = False

try:
    from torch.utils.tensorboard import SummaryWriter
    HAS_TENSORBOARD = True
except ImportError:
    HAS_TENSORBOARD = False

from dataset import create_data_loaders
from loss import create_loss
from model import create_model


def train_epoch(
    model: nn.Module,
    train_loader: torch.utils.data.DataLoader,
    criterion: nn.Module,
    optimizer: torch.optim.Optimizer,
    device: torch.device,
    epoch: int,
) -> dict[str, float]:
    """1エポックの学習を実行"""
    model.train()

    total_loss = 0.0
    loss_components = {"classification": 0.0, "location": 0.0, "depth": 0.0, "magnitude": 0.0}
    num_batches = 0

    pbar = tqdm(train_loader, desc=f"Epoch {epoch} [Train]")
    for batch in pbar:
        # データをデバイスに転送
        intensity = batch["intensity"].to(device)
        coords = batch["coords"].to(device)
        mask = batch["mask"].to(device)
        labels = {k: v.to(device) for k, v in batch["labels"].items()}

        # 順伝播
        optimizer.zero_grad()
        predictions = model(intensity, coords, mask)

        # 損失計算
        losses = criterion(predictions, labels)
        loss = losses["total"]

        # 逆伝播
        loss.backward()
        torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)
        optimizer.step()

        # 統計更新
        total_loss += loss.item()
        for key in loss_components:
            if key in losses:
                loss_components[key] += losses[key].item()
        num_batches += 1

        pbar.set_postfix(loss=f"{loss.item():.4f}")

    # 平均損失を計算
    avg_loss = total_loss / max(num_batches, 1)
    avg_components = {k: v / max(num_batches, 1) for k, v in loss_components.items()}

    return {"total": avg_loss, **avg_components}


@torch.no_grad()
def validate(
    model: nn.Module,
    val_loader: torch.utils.data.DataLoader,
    criterion: nn.Module,
    device: torch.device,
    epoch: int,
) -> dict[str, float]:
    """検証を実行"""
    model.eval()

    total_loss = 0.0
    loss_components = {"classification": 0.0, "location": 0.0, "depth": 0.0, "magnitude": 0.0}
    num_batches = 0

    # 評価指標用
    all_pred_cls = []
    all_target_cls = []
    all_distances = []

    pbar = tqdm(val_loader, desc=f"Epoch {epoch} [Val]")
    for batch in pbar:
        intensity = batch["intensity"].to(device)
        coords = batch["coords"].to(device)
        mask = batch["mask"].to(device)
        labels = {k: v.to(device) for k, v in batch["labels"].items()}

        predictions = model(intensity, coords, mask)
        losses = criterion(predictions, labels)

        total_loss += losses["total"].item()
        for key in loss_components:
            if key in losses:
                loss_components[key] += losses[key].item()
        num_batches += 1

        # 評価指標用データ収集
        pred_cls = predictions["has_earthquake"].squeeze(-1)
        all_pred_cls.append(pred_cls.cpu())
        all_target_cls.append(labels["has_earthquake"].cpu())

        # 距離誤差（地震ありサンプルのみ）
        eq_mask = labels["has_earthquake"] > 0.5
        if eq_mask.any():
            pred_lat = predictions["location"][:, 0]
            pred_lon = predictions["location"][:, 1]
            from loss import haversine_distance
            distances = haversine_distance(
                pred_lat[eq_mask],
                pred_lon[eq_mask],
                labels["latitude"][eq_mask],
                labels["longitude"][eq_mask],
            )
            all_distances.append(distances.cpu())

    # 平均損失
    avg_loss = total_loss / max(num_batches, 1)
    avg_components = {k: v / max(num_batches, 1) for k, v in loss_components.items()}

    # 評価指標
    all_pred_cls = torch.cat(all_pred_cls)
    all_target_cls = torch.cat(all_target_cls)

    # 分類精度
    pred_binary = (all_pred_cls > 0.5).float()
    accuracy = (pred_binary == all_target_cls).float().mean().item()

    # Precision, Recall, F1
    tp = ((pred_binary == 1) & (all_target_cls == 1)).sum().item()
    fp = ((pred_binary == 1) & (all_target_cls == 0)).sum().item()
    fn = ((pred_binary == 0) & (all_target_cls == 1)).sum().item()

    precision = tp / max(tp + fp, 1)
    recall = tp / max(tp + fn, 1)
    f1 = 2 * precision * recall / max(precision + recall, 1e-8)

    # 平均距離誤差
    if all_distances:
        all_distances = torch.cat(all_distances)
        mean_distance = all_distances.mean().item()
    else:
        mean_distance = 0.0

    return {
        "total": avg_loss,
        **avg_components,
        "accuracy": accuracy,
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "mean_distance_km": mean_distance,
    }


def save_checkpoint(
    model: nn.Module,
    optimizer: torch.optim.Optimizer,
    epoch: int,
    val_metrics: dict,
    checkpoint_dir: Path,
    is_best: bool = False,
) -> None:
    """チェックポイントを保存"""
    checkpoint_dir.mkdir(parents=True, exist_ok=True)

    checkpoint = {
        "epoch": epoch,
        "model_state_dict": model.state_dict(),
        "optimizer_state_dict": optimizer.state_dict(),
        "val_metrics": val_metrics,
    }

    # 通常のチェックポイント
    torch.save(checkpoint, checkpoint_dir / f"checkpoint_epoch_{epoch:04d}.pt")

    # 最新のチェックポイント
    torch.save(checkpoint, checkpoint_dir / "checkpoint_latest.pt")

    # ベストモデル
    if is_best:
        torch.save(checkpoint, checkpoint_dir / "checkpoint_best.pt")


def load_config(config_path: str | Path) -> dict:
    """設定ファイルを読み込む"""
    config_path = Path(config_path)

    if config_path.suffix in [".yaml", ".yml"]:
        if not HAS_YAML:
            raise ImportError("PyYAML is required to load YAML config files")
        with open(config_path, "r", encoding="utf-8") as f:
            return yaml.safe_load(f)
    elif config_path.suffix == ".json":
        with open(config_path, "r", encoding="utf-8") as f:
            return json.load(f)
    else:
        raise ValueError(f"Unsupported config file format: {config_path.suffix}")


def main():
    parser = argparse.ArgumentParser(description="地震震源・規模推定モデルの学習")

    # 設定ファイル
    parser.add_argument("--config", type=Path, help="設定ファイルのパス")

    # データ
    parser.add_argument("--data_dir", type=Path, help="データディレクトリ")
    parser.add_argument("--window_size", type=int, default=120, help="時間窓のサイズ（秒）")
    parser.add_argument("--train_ratio", type=float, default=0.8, help="訓練データの割合")

    # モデル
    parser.add_argument("--model_type", type=str, default="baseline",
                        choices=["baseline", "transformer"], help="モデルタイプ")
    parser.add_argument("--hidden_dim", type=int, default=256, help="隠れ層の次元数")
    parser.add_argument("--num_layers", type=int, default=4, help="レイヤー数")
    parser.add_argument("--num_heads", type=int, default=8, help="Attentionヘッド数")
    parser.add_argument("--dropout", type=float, default=0.1, help="ドロップアウト率")

    # 学習
    parser.add_argument("--batch_size", type=int, default=32, help="バッチサイズ")
    parser.add_argument("--epochs", type=int, default=100, help="エポック数")
    parser.add_argument("--lr", type=float, default=0.001, help="学習率")
    parser.add_argument("--weight_decay", type=float, default=0.01, help="Weight decay")
    parser.add_argument("--early_stopping", type=int, default=10, help="Early stopping patience")

    # 損失関数
    parser.add_argument("--loss_type", type=str, default="standard",
                        choices=["standard", "focal"], help="損失関数タイプ")

    # 出力
    parser.add_argument("--output_dir", type=Path, default=Path("output"),
                        help="出力ディレクトリ")
    parser.add_argument("--experiment_name", type=str, default=None, help="実験名")

    # その他
    parser.add_argument("--seed", type=int, default=42, help="乱数シード")
    parser.add_argument("--num_workers", type=int, default=0, help="DataLoaderのワーカー数")
    parser.add_argument("--device", type=str, default="auto", help="デバイス（auto/cpu/cuda）")

    args = parser.parse_args()

    # 設定ファイルがあれば読み込んで上書き
    if args.config:
        config = load_config(args.config)
        for key, value in config.items():
            if isinstance(value, dict):
                for sub_key, sub_value in value.items():
                    if hasattr(args, sub_key):
                        setattr(args, sub_key, sub_value)
            elif hasattr(args, key):
                setattr(args, key, value)

    # データディレクトリの確認
    if not args.data_dir:
        print("エラー: --data_dir を指定してください", file=sys.stderr)
        sys.exit(1)

    # デバイス設定
    if args.device == "auto":
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    else:
        device = torch.device(args.device)
    print(f"デバイス: {device}")

    # 乱数シード
    torch.manual_seed(args.seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(args.seed)

    # 実験名
    if args.experiment_name is None:
        args.experiment_name = datetime.now().strftime("%Y%m%d_%H%M%S")

    # 出力ディレクトリ
    experiment_dir = args.output_dir / args.experiment_name
    checkpoint_dir = experiment_dir / "checkpoints"
    log_dir = experiment_dir / "logs"
    experiment_dir.mkdir(parents=True, exist_ok=True)

    # 設定を保存
    with open(experiment_dir / "config.json", "w", encoding="utf-8") as f:
        json.dump(vars(args), f, indent=2, default=str)

    print(f"実験ディレクトリ: {experiment_dir}")

    # データローダー
    print("データを読み込み中...")
    train_loader, val_loader, dataset_info = create_data_loaders(
        data_dir=args.data_dir,
        batch_size=args.batch_size,
        window_size=args.window_size,
        train_ratio=args.train_ratio,
        num_workers=args.num_workers,
        seed=args.seed,
    )
    print(f"訓練サンプル数: {dataset_info['train_samples']}")
    print(f"検証サンプル数: {dataset_info['val_samples']}")

    # モデル
    print(f"モデルを作成中... ({args.model_type})")
    model = create_model(
        model_type=args.model_type,
        hidden_dim=args.hidden_dim,
        num_layers=args.num_layers,
        num_heads=args.num_heads,
        dropout=args.dropout,
    )
    model = model.to(device)

    # パラメータ数を表示
    num_params = sum(p.numel() for p in model.parameters() if p.requires_grad)
    print(f"パラメータ数: {num_params:,}")

    # 損失関数
    criterion = create_loss(loss_type=args.loss_type)

    # オプティマイザ
    optimizer = AdamW(model.parameters(), lr=args.lr, weight_decay=args.weight_decay)

    # スケジューラ
    scheduler = CosineAnnealingLR(optimizer, T_max=args.epochs, eta_min=args.lr * 0.01)

    # TensorBoard
    writer = None
    if HAS_TENSORBOARD:
        writer = SummaryWriter(log_dir=log_dir)

    # 学習ループ
    best_val_loss = float("inf")
    patience_counter = 0

    print("学習を開始...")
    for epoch in range(1, args.epochs + 1):
        # 学習
        train_metrics = train_epoch(model, train_loader, criterion, optimizer, device, epoch)

        # 検証
        val_metrics = validate(model, val_loader, criterion, device, epoch)

        # スケジューラ更新
        scheduler.step()

        # ログ出力
        print(f"\nEpoch {epoch}/{args.epochs}")
        print(f"  Train Loss: {train_metrics['total']:.4f}")
        print(f"  Val Loss: {val_metrics['total']:.4f}")
        print(f"  Val Accuracy: {val_metrics['accuracy']:.4f}")
        print(f"  Val F1: {val_metrics['f1']:.4f}")
        print(f"  Val Distance Error: {val_metrics['mean_distance_km']:.2f} km")

        # TensorBoard
        if writer:
            writer.add_scalar("Loss/train", train_metrics["total"], epoch)
            writer.add_scalar("Loss/val", val_metrics["total"], epoch)
            writer.add_scalar("Metrics/accuracy", val_metrics["accuracy"], epoch)
            writer.add_scalar("Metrics/f1", val_metrics["f1"], epoch)
            writer.add_scalar("Metrics/distance_km", val_metrics["mean_distance_km"], epoch)
            writer.add_scalar("LR", optimizer.param_groups[0]["lr"], epoch)

        # チェックポイント保存
        is_best = val_metrics["total"] < best_val_loss
        if is_best:
            best_val_loss = val_metrics["total"]
            patience_counter = 0
        else:
            patience_counter += 1

        save_checkpoint(model, optimizer, epoch, val_metrics, checkpoint_dir, is_best)

        # Early stopping
        if patience_counter >= args.early_stopping:
            print(f"\nEarly stopping at epoch {epoch}")
            break

    print(f"\n学習完了！ベスト検証損失: {best_val_loss:.4f}")

    if writer:
        writer.close()


if __name__ == "__main__":
    main()
