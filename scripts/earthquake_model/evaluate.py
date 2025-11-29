#!/usr/bin/env python3
"""
評価スクリプト

モデルの性能を評価し、詳細なメトリクスを出力する。

使用方法:
    python evaluate.py --checkpoint checkpoint_best.pt --data_dir ./data
"""

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn
from tqdm import tqdm

from dataset import EarthquakeDataset, collate_fn
from loss import haversine_distance
from model import create_model


@torch.no_grad()
def evaluate_model(
    model: nn.Module,
    data_loader: torch.utils.data.DataLoader,
    device: torch.device,
) -> dict:
    """
    モデルを評価する

    Returns:
        評価結果の辞書
    """
    model.eval()

    # 収集用リスト
    all_pred_cls = []
    all_target_cls = []
    all_pred_lat = []
    all_pred_lon = []
    all_pred_depth = []
    all_pred_mag = []
    all_target_lat = []
    all_target_lon = []
    all_target_depth = []
    all_target_mag = []
    all_eq_mask = []

    for batch in tqdm(data_loader, desc="Evaluating"):
        intensity = batch["intensity"].to(device)
        coords = batch["coords"].to(device)
        mask = batch["mask"].to(device)
        labels = batch["labels"]

        predictions = model(intensity, coords, mask)

        # 予測値
        all_pred_cls.append(predictions["has_earthquake"].squeeze(-1).cpu())
        all_pred_lat.append(predictions["location"][:, 0].cpu())
        all_pred_lon.append(predictions["location"][:, 1].cpu())
        all_pred_depth.append(predictions["location"][:, 2].cpu())
        all_pred_mag.append(predictions["magnitude"].squeeze(-1).cpu())

        # 正解値
        all_target_cls.append(labels["has_earthquake"])
        all_target_lat.append(labels["latitude"])
        all_target_lon.append(labels["longitude"])
        all_target_depth.append(labels["depth"])
        all_target_mag.append(labels["magnitude"])
        all_eq_mask.append(labels["has_earthquake"] > 0.5)

    # 結合
    pred_cls = torch.cat(all_pred_cls)
    target_cls = torch.cat(all_target_cls)
    pred_lat = torch.cat(all_pred_lat)
    pred_lon = torch.cat(all_pred_lon)
    pred_depth = torch.cat(all_pred_depth)
    pred_mag = torch.cat(all_pred_mag)
    target_lat = torch.cat(all_target_lat)
    target_lon = torch.cat(all_target_lon)
    target_depth = torch.cat(all_target_depth)
    target_mag = torch.cat(all_target_mag)
    eq_mask = torch.cat(all_eq_mask)

    # =====================================
    # 分類メトリクス
    # =====================================
    pred_binary = (pred_cls > 0.5).float()

    # 混同行列
    tp = ((pred_binary == 1) & (target_cls == 1)).sum().item()
    tn = ((pred_binary == 0) & (target_cls == 0)).sum().item()
    fp = ((pred_binary == 1) & (target_cls == 0)).sum().item()
    fn = ((pred_binary == 0) & (target_cls == 1)).sum().item()

    # メトリクス
    accuracy = (tp + tn) / max(tp + tn + fp + fn, 1)
    precision = tp / max(tp + fp, 1)
    recall = tp / max(tp + fn, 1)
    f1 = 2 * precision * recall / max(precision + recall, 1e-8)
    specificity = tn / max(tn + fp, 1)

    classification_metrics = {
        "accuracy": accuracy,
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "specificity": specificity,
        "confusion_matrix": {
            "tp": int(tp),
            "tn": int(tn),
            "fp": int(fp),
            "fn": int(fn),
        },
    }

    # =====================================
    # 位置メトリクス（地震ありサンプルのみ）
    # =====================================
    if eq_mask.any():
        # 水平距離誤差
        distances = haversine_distance(
            pred_lat[eq_mask],
            pred_lon[eq_mask],
            target_lat[eq_mask],
            target_lon[eq_mask],
        )
        distances_np = distances.numpy()

        location_metrics = {
            "mean_distance_km": float(np.mean(distances_np)),
            "median_distance_km": float(np.median(distances_np)),
            "std_distance_km": float(np.std(distances_np)),
            "min_distance_km": float(np.min(distances_np)),
            "max_distance_km": float(np.max(distances_np)),
            "distance_percentiles": {
                "p25": float(np.percentile(distances_np, 25)),
                "p50": float(np.percentile(distances_np, 50)),
                "p75": float(np.percentile(distances_np, 75)),
                "p90": float(np.percentile(distances_np, 90)),
                "p95": float(np.percentile(distances_np, 95)),
            },
        }

        # 深さ誤差
        depth_errors = (pred_depth[eq_mask] - target_depth[eq_mask]).abs().numpy()
        depth_metrics = {
            "mean_error_km": float(np.mean(depth_errors)),
            "median_error_km": float(np.median(depth_errors)),
            "std_error_km": float(np.std(depth_errors)),
        }

        # マグニチュード誤差
        mag_errors = (pred_mag[eq_mask] - target_mag[eq_mask]).abs().numpy()
        magnitude_metrics = {
            "mean_error": float(np.mean(mag_errors)),
            "median_error": float(np.median(mag_errors)),
            "std_error": float(np.std(mag_errors)),
        }
    else:
        location_metrics = {"error": "地震ありサンプルなし"}
        depth_metrics = {"error": "地震ありサンプルなし"}
        magnitude_metrics = {"error": "地震ありサンプルなし"}

    return {
        "num_samples": len(pred_cls),
        "num_earthquake_samples": int(eq_mask.sum().item()),
        "classification": classification_metrics,
        "location": location_metrics,
        "depth": depth_metrics,
        "magnitude": magnitude_metrics,
    }


def print_results(results: dict) -> None:
    """評価結果を表示する"""
    print("\n" + "=" * 60)
    print("評価結果")
    print("=" * 60)

    print(f"\nサンプル数: {results['num_samples']}")
    print(f"地震ありサンプル数: {results['num_earthquake_samples']}")

    print("\n--- 分類メトリクス ---")
    cls = results["classification"]
    print(f"  Accuracy:    {cls['accuracy']:.4f}")
    print(f"  Precision:   {cls['precision']:.4f}")
    print(f"  Recall:      {cls['recall']:.4f}")
    print(f"  F1 Score:    {cls['f1']:.4f}")
    print(f"  Specificity: {cls['specificity']:.4f}")
    cm = cls["confusion_matrix"]
    print(f"\n  混同行列:")
    print(f"    TP: {cm['tp']:5d}  FP: {cm['fp']:5d}")
    print(f"    FN: {cm['fn']:5d}  TN: {cm['tn']:5d}")

    if "error" not in results["location"]:
        print("\n--- 震源位置 ---")
        loc = results["location"]
        print(f"  平均距離誤差:   {loc['mean_distance_km']:.2f} km")
        print(f"  中央距離誤差:   {loc['median_distance_km']:.2f} km")
        print(f"  標準偏差:       {loc['std_distance_km']:.2f} km")
        print(f"  最小/最大:      {loc['min_distance_km']:.2f} / {loc['max_distance_km']:.2f} km")
        pct = loc["distance_percentiles"]
        print(f"  パーセンタイル: P50={pct['p50']:.1f}, P90={pct['p90']:.1f}, P95={pct['p95']:.1f} km")

        print("\n--- 深さ ---")
        dep = results["depth"]
        print(f"  平均誤差:   {dep['mean_error_km']:.2f} km")
        print(f"  中央誤差:   {dep['median_error_km']:.2f} km")
        print(f"  標準偏差:   {dep['std_error_km']:.2f} km")

        print("\n--- マグニチュード ---")
        mag = results["magnitude"]
        print(f"  平均誤差:   {mag['mean_error']:.3f}")
        print(f"  中央誤差:   {mag['median_error']:.3f}")
        print(f"  標準偏差:   {mag['std_error']:.3f}")

    print("\n" + "=" * 60)


def main():
    parser = argparse.ArgumentParser(description="モデルの評価")

    parser.add_argument("--checkpoint", type=Path, required=True,
                        help="チェックポイントファイルのパス")
    parser.add_argument("--data_dir", type=Path, required=True,
                        help="データディレクトリ")
    parser.add_argument("--output", type=Path, default=None,
                        help="結果を保存するJSONファイル")

    # データ
    parser.add_argument("--window_size", type=int, default=120,
                        help="時間窓のサイズ")
    parser.add_argument("--batch_size", type=int, default=32,
                        help="バッチサイズ")

    # モデル（チェックポイントから推測できない場合）
    parser.add_argument("--model_type", type=str, default="baseline",
                        choices=["baseline", "transformer"])
    parser.add_argument("--hidden_dim", type=int, default=256)
    parser.add_argument("--num_layers", type=int, default=4)
    parser.add_argument("--num_heads", type=int, default=8)

    parser.add_argument("--device", type=str, default="auto")

    args = parser.parse_args()

    # デバイス
    if args.device == "auto":
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    else:
        device = torch.device(args.device)
    print(f"デバイス: {device}")

    # チェックポイントの読み込み
    print(f"チェックポイントを読み込み中: {args.checkpoint}")
    checkpoint = torch.load(args.checkpoint, map_location=device, weights_only=False)

    # 設定ファイルがあれば読み込む
    config_path = args.checkpoint.parent.parent / "config.json"
    if config_path.exists():
        with open(config_path, "r", encoding="utf-8") as f:
            config = json.load(f)
        args.model_type = config.get("model_type", args.model_type)
        args.hidden_dim = config.get("hidden_dim", args.hidden_dim)
        args.num_layers = config.get("num_layers", args.num_layers)
        args.num_heads = config.get("num_heads", args.num_heads)
        args.window_size = config.get("window_size", args.window_size)

    # モデルの作成
    print(f"モデルを作成中... ({args.model_type})")
    model = create_model(
        model_type=args.model_type,
        hidden_dim=args.hidden_dim,
        num_layers=args.num_layers,
        num_heads=args.num_heads,
    )
    model.load_state_dict(checkpoint["model_state_dict"])
    model = model.to(device)

    # データセット（全ファイルを評価用に使用）
    print("データを読み込み中...")
    npz_files = sorted(args.data_dir.glob("**/dataset.npz"))
    if not npz_files:
        print(f"エラー: npzファイルが見つかりません: {args.data_dir}", file=sys.stderr)
        sys.exit(1)

    dataset = EarthquakeDataset(
        file_paths=npz_files,
        window_size=args.window_size,
        include_no_earthquake=True,
    )
    data_loader = torch.utils.data.DataLoader(
        dataset,
        batch_size=args.batch_size,
        shuffle=False,
        collate_fn=collate_fn,
    )

    print(f"サンプル数: {len(dataset)}")

    # 評価
    results = evaluate_model(model, data_loader, device)

    # 結果を表示
    print_results(results)

    # 結果を保存
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with open(args.output, "w", encoding="utf-8") as f:
            json.dump(results, f, indent=2, ensure_ascii=False)
        print(f"\n結果を保存しました: {args.output}")


if __name__ == "__main__":
    main()
