"""
地震データセットクラス

npz形式のデータを読み込み、学習用のサンプルを生成する。
"""

import random
from pathlib import Path
from typing import Any

import numpy as np
import torch
from torch.utils.data import Dataset


class EarthquakeDataset(Dataset):
    """
    地震データセット

    Parameters:
        file_paths: npzファイルのパスリスト
        window_size: 時間窓の長さ（秒）
        include_no_earthquake: 地震なしサンプルを含めるか
        no_earthquake_ratio: 地震なしサンプルの比率（地震ありに対する倍率）
        earthquake_augment_count: 地震ありサンプルの時間シフト拡張数
        earthquake_time_shift_range: 地震発生位置の時間シフト範囲（秒）
        no_earthquake_stride: 地震なしサンプルの生成間隔（秒）
        post_event_margin: 地震発生後に除外するマージン（秒）
        transform: データ変換関数（オプション）
    """

    def __init__(
        self,
        file_paths: list[str | Path],
        window_size: int = 120,
        include_no_earthquake: bool = True,
        no_earthquake_ratio: float = 10.0,
        earthquake_augment_count: int = 10,
        earthquake_time_shift_range: tuple[int, int] = (5, 60),
        no_earthquake_stride: int = 1,
        post_event_margin: int = 60,
        transform: Any = None,
    ):
        self.file_paths = [Path(p) for p in file_paths]
        self.window_size = window_size
        self.include_no_earthquake = include_no_earthquake
        self.no_earthquake_ratio = no_earthquake_ratio
        self.earthquake_augment_count = earthquake_augment_count
        self.earthquake_time_shift_range = earthquake_time_shift_range
        self.no_earthquake_stride = no_earthquake_stride
        self.post_event_margin = post_event_margin
        self.transform = transform

        # データをロード
        self.data_list: list[dict] = []
        self.samples: list[dict] = []

        self._load_all_data()
        self._build_samples()

    def _load_all_data(self) -> None:
        """全npzファイルを読み込む"""
        for file_path in self.file_paths:
            if not file_path.exists():
                print(f"警告: ファイルが見つかりません: {file_path}")
                continue

            data = np.load(file_path, allow_pickle=True)
            self.data_list.append({
                "file_path": file_path,
                "intensity": data["intensity"],  # (total_frames, station_count)
                "coords": data["coords"],  # (station_count, 2)
                "station_mask": data["station_mask"],  # (station_count,)
                "earthquakes": data["earthquakes"].tolist(),  # list[dict]
                "metadata": data["metadata"].tolist(),  # dict
            })

    def _calculate_post_event_margin(self, magnitude: float) -> int:
        """
        マグニチュードに応じた地震後の除外マージンを計算する

        大きな地震ほど揺れが長く続くため、マージンを大きくする。
        M4未満: 60秒
        M4-5: 90秒
        M5-6: 120秒
        M6-7: 180秒
        M7以上: 300秒
        """
        if magnitude < 4.0:
            return 60
        elif magnitude < 5.0:
            return 90
        elif magnitude < 6.0:
            return 120
        elif magnitude < 7.0:
            return 180
        else:
            return 300

    def _build_samples(self) -> None:
        """サンプルリストを構築する"""
        self.samples = []

        for data_idx, data in enumerate(self.data_list):
            total_frames = data["intensity"].shape[0]
            earthquakes = data["earthquakes"]

            # 地震発生時刻の周辺をマークする（地震なしサンプル除外用）
            earthquake_zones = set()
            for eq in earthquakes:
                t = eq["timeIndex"]
                magnitude = eq.get("magnitude", 5.0)  # デフォルトM5
                margin = self._calculate_post_event_margin(magnitude)
                # 地震発生前後の範囲を除外ゾーンに追加
                for offset in range(-self.window_size, margin + self.window_size):
                    if 0 <= t + offset < total_frames:
                        earthquake_zones.add(t + offset)

            # 地震ありサンプル（時間シフトで拡張）
            for eq in earthquakes:
                time_index = eq["timeIndex"]

                # 複数の時間シフトでサンプルを生成
                for _ in range(self.earthquake_augment_count):
                    # 地震発生位置をwindow内でランダムにシフト
                    min_shift, max_shift = self.earthquake_time_shift_range
                    event_position_in_window = random.randint(min_shift, min(max_shift, self.window_size - 10))

                    start_index = time_index - event_position_in_window

                    # 範囲チェック
                    if start_index < 0:
                        start_index = 0
                        event_position_in_window = time_index
                    if start_index + self.window_size > total_frames:
                        start_index = max(0, total_frames - self.window_size)
                        event_position_in_window = time_index - start_index

                    self.samples.append({
                        "data_idx": data_idx,
                        "start_index": start_index,
                        "has_earthquake": True,
                        "earthquake": eq,
                        "relative_time_index": event_position_in_window,
                    })

            # 地震なしサンプル（スライディングウィンドウで生成）
            if self.include_no_earthquake:
                no_eq_samples = []

                # ストライドごとにサンプル候補を生成
                for start in range(0, total_frames - self.window_size, self.no_earthquake_stride):
                    # 窓内に地震発生ゾーンが含まれないかチェック
                    window_range = set(range(start, start + self.window_size))
                    if not window_range & earthquake_zones:
                        no_eq_samples.append(start)

                # 地震ありサンプル数に対する比率でサンプリング
                num_eq_samples = len(earthquakes) * self.earthquake_augment_count
                max_no_eq_samples = int(num_eq_samples * self.no_earthquake_ratio)

                if no_eq_samples:
                    # サンプル数を制限（ただし候補が少ない場合は全部使う）
                    if len(no_eq_samples) > max_no_eq_samples:
                        selected_starts = random.sample(no_eq_samples, max_no_eq_samples)
                    else:
                        selected_starts = no_eq_samples

                    for start_index in selected_starts:
                        self.samples.append({
                            "data_idx": data_idx,
                            "start_index": start_index,
                            "has_earthquake": False,
                            "earthquake": None,
                            "relative_time_index": None,
                        })

        # サンプルをシャッフル
        random.shuffle(self.samples)

    def __len__(self) -> int:
        return len(self.samples)

    def __getitem__(self, idx: int) -> dict:
        sample = self.samples[idx]
        data = self.data_list[sample["data_idx"]]

        start = sample["start_index"]
        end = start + self.window_size

        # 震度データを切り出し
        intensity = data["intensity"][start:end].copy()  # (window_size, station_count)

        # NaNを-3.0（震度計測範囲外）で埋める
        intensity = np.nan_to_num(intensity, nan=-3.0)

        # 座標データ
        coords = data["coords"].copy()  # (station_count, 2)

        # マスク
        mask = data["station_mask"].copy()  # (station_count,)

        # ラベル
        if sample["has_earthquake"] and sample["earthquake"]:
            eq = sample["earthquake"]
            label = {
                "has_earthquake": True,
                "latitude": eq.get("latitude"),
                "longitude": eq.get("longitude"),
                "depth": eq.get("depth"),
                "magnitude": eq.get("magnitude"),
                "relative_time_index": sample["relative_time_index"],
            }
        else:
            label = {
                "has_earthquake": False,
                "latitude": None,
                "longitude": None,
                "depth": None,
                "magnitude": None,
                "relative_time_index": None,
            }

        result = {
            "intensity": torch.tensor(intensity, dtype=torch.float32),
            "coords": torch.tensor(coords, dtype=torch.float32),
            "mask": torch.tensor(mask, dtype=torch.bool),
            "label": label,
        }

        if self.transform:
            result = self.transform(result)

        return result

    def get_stats(self) -> dict:
        """データセットの統計情報を取得"""
        total_samples = len(self.samples)
        eq_samples = sum(1 for s in self.samples if s["has_earthquake"])
        no_eq_samples = total_samples - eq_samples

        # ファイルごとの地震数を集計
        total_earthquakes = sum(len(d["earthquakes"]) for d in self.data_list)

        return {
            "total_samples": total_samples,
            "earthquake_samples": eq_samples,
            "no_earthquake_samples": no_eq_samples,
            "num_files": len(self.data_list),
            "total_earthquakes": total_earthquakes,
            "window_size": self.window_size,
            "augment_count": self.earthquake_augment_count,
        }


def collate_fn(batch: list[dict]) -> dict:
    """
    可変長の観測点をパディングしてバッチ化

    各サンプルの観測点数が異なる場合に対応するが、
    現状は同じ観測点セットを使用しているため、単純なスタックで十分。

    Returns:
        {
            "intensity": Tensor (batch, window_size, max_stations),
            "coords": Tensor (batch, max_stations, 2),
            "mask": Tensor (batch, max_stations),
            "labels": バッチ化されたラベル
        }
    """
    # 最大観測点数を取得
    max_stations = max(sample["intensity"].shape[1] for sample in batch)
    window_size = batch[0]["intensity"].shape[0]
    batch_size = len(batch)

    # バッチテンソルを初期化
    intensity_batch = torch.full(
        (batch_size, window_size, max_stations),
        fill_value=-3.0,  # パディング値
        dtype=torch.float32
    )
    coords_batch = torch.zeros(
        (batch_size, max_stations, 2),
        dtype=torch.float32
    )
    mask_batch = torch.zeros(
        (batch_size, max_stations),
        dtype=torch.bool
    )

    # ラベルをバッチ化
    labels = {
        "has_earthquake": [],
        "latitude": [],
        "longitude": [],
        "depth": [],
        "magnitude": [],
        "relative_time_index": [],
    }

    for i, sample in enumerate(batch):
        num_stations = sample["intensity"].shape[1]

        intensity_batch[i, :, :num_stations] = sample["intensity"]
        coords_batch[i, :num_stations, :] = sample["coords"]
        mask_batch[i, :num_stations] = sample["mask"]

        label = sample["label"]
        labels["has_earthquake"].append(label["has_earthquake"])
        labels["latitude"].append(label["latitude"] if label["latitude"] is not None else 0.0)
        labels["longitude"].append(label["longitude"] if label["longitude"] is not None else 0.0)
        labels["depth"].append(label["depth"] if label["depth"] is not None else 0.0)
        labels["magnitude"].append(label["magnitude"] if label["magnitude"] is not None else 0.0)
        labels["relative_time_index"].append(
            label["relative_time_index"] if label["relative_time_index"] is not None else -1
        )

    # ラベルをテンソル化
    labels_tensor = {
        "has_earthquake": torch.tensor(labels["has_earthquake"], dtype=torch.float32),
        "latitude": torch.tensor(labels["latitude"], dtype=torch.float32),
        "longitude": torch.tensor(labels["longitude"], dtype=torch.float32),
        "depth": torch.tensor(labels["depth"], dtype=torch.float32),
        "magnitude": torch.tensor(labels["magnitude"], dtype=torch.float32),
        "relative_time_index": torch.tensor(labels["relative_time_index"], dtype=torch.long),
    }

    return {
        "intensity": intensity_batch,
        "coords": coords_batch,
        "mask": mask_batch,
        "labels": labels_tensor,
    }


def create_data_loaders(
    data_dir: str | Path,
    batch_size: int = 32,
    window_size: int = 120,
    train_ratio: float = 0.8,
    num_workers: int = 0,
    seed: int = 42,
    no_earthquake_ratio: float = 10.0,
    earthquake_augment_count: int = 10,
) -> tuple:
    """
    データローダーを作成する

    Args:
        data_dir: npzファイルが格納されたディレクトリ
        batch_size: バッチサイズ
        window_size: 時間窓のサイズ
        train_ratio: 訓練データの割合
        num_workers: DataLoaderのワーカー数
        seed: 乱数シード
        no_earthquake_ratio: 地震なしサンプルの比率
        earthquake_augment_count: 地震ありサンプルの拡張数

    Returns:
        (train_loader, val_loader, dataset_info)
    """
    data_dir = Path(data_dir)
    npz_files = sorted(data_dir.glob("**/dataset.npz"))

    if not npz_files:
        raise FileNotFoundError(f"npzファイルが見つかりません: {data_dir}")

    # シャッフルして分割
    random.seed(seed)
    shuffled_files = list(npz_files)
    random.shuffle(shuffled_files)

    split_idx = int(len(shuffled_files) * train_ratio)
    train_files = shuffled_files[:split_idx]
    val_files = shuffled_files[split_idx:]

    # 最低1ファイルは検証用に確保
    if not val_files and len(shuffled_files) > 1:
        val_files = [train_files.pop()]
    elif not val_files:
        val_files = train_files.copy()

    # データセットを作成
    train_dataset = EarthquakeDataset(
        file_paths=train_files,
        window_size=window_size,
        include_no_earthquake=True,
        no_earthquake_ratio=no_earthquake_ratio,
        earthquake_augment_count=earthquake_augment_count,
    )
    val_dataset = EarthquakeDataset(
        file_paths=val_files,
        window_size=window_size,
        include_no_earthquake=True,
        no_earthquake_ratio=no_earthquake_ratio,
        earthquake_augment_count=earthquake_augment_count // 2 or 1,  # 検証用は少なめ
    )

    # データローダーを作成
    train_loader = torch.utils.data.DataLoader(
        train_dataset,
        batch_size=batch_size,
        shuffle=True,
        collate_fn=collate_fn,
        num_workers=num_workers,
        pin_memory=torch.cuda.is_available(),
    )
    val_loader = torch.utils.data.DataLoader(
        val_dataset,
        batch_size=batch_size,
        shuffle=False,
        collate_fn=collate_fn,
        num_workers=num_workers,
        pin_memory=torch.cuda.is_available(),
    )

    dataset_info = {
        "train_files": len(train_files),
        "val_files": len(val_files),
        "train_samples": len(train_dataset),
        "val_samples": len(val_dataset),
        "train_stats": train_dataset.get_stats(),
        "val_stats": val_dataset.get_stats(),
    }

    return train_loader, val_loader, dataset_info
