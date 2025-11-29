#!/usr/bin/env python3
"""
中間データをnpz形式に変換するスクリプト

使用方法:
    # 単一ディレクトリを変換
    python convert_to_npz.py <input_dir>

    # 親ディレクトリを指定して子フォルダをまとめて変換
    python convert_to_npz.py <parent_dir> --batch

入力:
    - metadata.json: メタデータ（観測点情報、時間範囲）
    - intensity.bin: 震度データ（float32バイナリ）
    - earthquakes.json: 地震情報

出力:
    - dataset.npz: NumPy形式のデータセット（各入力ディレクトリ内に生成）
"""

import argparse
import json
import sys
from pathlib import Path

import numpy as np


def is_dataset_dir(path: Path) -> bool:
    """データセットディレクトリかどうかを判定する"""
    return (
        path.is_dir()
        and (path / "metadata.json").exists()
        and (path / "intensity.bin").exists()
        and (path / "earthquakes.json").exists()
    )


def find_dataset_dirs(parent_dir: Path) -> list[Path]:
    """親ディレクトリから子フォルダ内のデータセットディレクトリを探す"""
    dataset_dirs = []

    # 親ディレクトリ自体がデータセットの場合
    if is_dataset_dir(parent_dir):
        dataset_dirs.append(parent_dir)
        return dataset_dirs

    # 子ディレクトリを探索
    for child in sorted(parent_dir.iterdir()):
        if is_dataset_dir(child):
            dataset_dirs.append(child)

    return dataset_dirs


def load_metadata(input_dir: Path) -> dict:
    """メタデータを読み込む"""
    metadata_path = input_dir / "metadata.json"
    if not metadata_path.exists():
        raise FileNotFoundError(f"metadata.json が見つかりません: {metadata_path}")

    with open(metadata_path, "r", encoding="utf-8") as f:
        return json.load(f)


def load_intensity(input_dir: Path, total_frames: int, station_count: int) -> np.ndarray:
    """
    震度データを読み込む

    Returns:
        intensity: shape (total_frames, station_count), dtype float32
    """
    intensity_path = input_dir / "intensity.bin"
    if not intensity_path.exists():
        raise FileNotFoundError(f"intensity.bin が見つかりません: {intensity_path}")

    # バイナリファイルを読み込み
    expected_size = total_frames * station_count * 4  # float32 = 4 bytes
    with open(intensity_path, "rb") as f:
        data = f.read()

    if len(data) != expected_size:
        raise ValueError(
            f"ファイルサイズが不正です。期待: {expected_size} bytes, 実際: {len(data)} bytes"
        )

    # NumPy配列に変換
    intensity = np.frombuffer(data, dtype=np.float32)
    intensity = intensity.reshape(total_frames, station_count)

    return intensity


def load_earthquakes(input_dir: Path) -> list[dict]:
    """地震情報を読み込む"""
    earthquakes_path = input_dir / "earthquakes.json"
    if not earthquakes_path.exists():
        raise FileNotFoundError(f"earthquakes.json が見つかりません: {earthquakes_path}")

    with open(earthquakes_path, "r", encoding="utf-8") as f:
        return json.load(f)


def convert_to_npz(input_dir: Path, output_file: Path, verbose: bool = True) -> bool:
    """
    中間データをnpz形式に変換する

    Returns:
        bool: 変換が成功したかどうか
    """
    if verbose:
        print(f"入力ディレクトリ: {input_dir}")
        print(f"出力ファイル: {output_file}")

    # メタデータを読み込み
    if verbose:
        print("メタデータを読み込み中...")
    metadata = load_metadata(input_dir)
    total_frames = metadata["totalFrames"]
    station_count = metadata["stationCount"]
    stations = metadata["stations"]

    if verbose:
        print(f"  総フレーム数: {total_frames}")
        print(f"  観測点数: {station_count}")
        print(f"  開始時刻: {metadata['startTime']}")
        print(f"  終了時刻: {metadata['endTime']}")

    # 震度データを読み込み
    if verbose:
        print("震度データを読み込み中...")
    intensity = load_intensity(input_dir, total_frames, station_count)
    if verbose:
        print(f"  震度データ shape: {intensity.shape}")

    # 欠損値の統計
    nan_count = np.isnan(intensity).sum()
    total_count = intensity.size
    nan_ratio = nan_count / total_count * 100
    if verbose:
        print(f"  欠損値: {nan_count}/{total_count} ({nan_ratio:.2f}%)")

    # 座標データを作成
    coords = np.array(
        [[s["lat"], s["lon"]] for s in stations],
        dtype=np.float32,
    )
    if verbose:
        print(f"  座標データ shape: {coords.shape}")

    # 観測点マスクを作成（全フレームで有効なデータがある観測点）
    # ここでは単純に、1フレームでも有効なデータがあればTrueとする
    station_mask = ~np.all(np.isnan(intensity), axis=0)
    valid_station_count = station_mask.sum()
    if verbose:
        print(f"  有効観測点数: {valid_station_count}/{station_count}")

    # 地震情報を読み込み
    if verbose:
        print("地震情報を読み込み中...")
    earthquakes = load_earthquakes(input_dir)
    if verbose:
        print(f"  地震数: {len(earthquakes)}")

    # npzに保存
    if verbose:
        print("npzファイルを保存中...")
    np.savez_compressed(
        output_file,
        intensity=intensity,
        coords=coords,
        station_mask=station_mask,
        earthquakes=earthquakes,
        metadata=metadata,
    )

    # ファイルサイズを確認
    file_size = output_file.stat().st_size / (1024 * 1024)
    if verbose:
        print(f"保存完了: {output_file} ({file_size:.2f} MB)")

    return True


def convert_batch(parent_dir: Path, skip_existing: bool = True) -> tuple[int, int]:
    """
    親ディレクトリ内の全データセットをまとめて変換する

    Args:
        parent_dir: 親ディレクトリ
        skip_existing: 既に変換済みのものをスキップするか

    Returns:
        tuple[int, int]: (成功数, 失敗数)
    """
    dataset_dirs = find_dataset_dirs(parent_dir)

    if not dataset_dirs:
        print(f"データセットが見つかりません: {parent_dir}", file=sys.stderr)
        return 0, 0

    print(f"変換対象: {len(dataset_dirs)} ディレクトリ")
    print("-" * 60)

    success_count = 0
    fail_count = 0

    for i, dataset_dir in enumerate(dataset_dirs, 1):
        output_file = dataset_dir / "dataset.npz"
        dir_name = dataset_dir.name

        # 既存ファイルのスキップ
        if skip_existing and output_file.exists():
            print(f"[{i}/{len(dataset_dirs)}] {dir_name}: スキップ（変換済み）")
            success_count += 1
            continue

        print(f"[{i}/{len(dataset_dirs)}] {dir_name}: 変換中...")

        try:
            convert_to_npz(dataset_dir, output_file, verbose=False)
            file_size = output_file.stat().st_size / (1024 * 1024)
            print(f"  -> 完了 ({file_size:.2f} MB)")
            success_count += 1
        except Exception as e:
            print(f"  -> 失敗: {e}", file=sys.stderr)
            fail_count += 1

    print("-" * 60)
    print(f"変換完了: 成功 {success_count}, 失敗 {fail_count}")

    return success_count, fail_count


def main():
    parser = argparse.ArgumentParser(
        description="中間データをnpz形式に変換する",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
例:
    # 単一ディレクトリを変換
    python convert_to_npz.py ./dataset_20240101_120000

    # 親ディレクトリを指定して子フォルダをまとめて変換
    python convert_to_npz.py ./datasets --batch

    # 変換済みも含めて全て再変換
    python convert_to_npz.py ./datasets --batch --force
        """,
    )
    parser.add_argument(
        "input_dir",
        type=Path,
        help="中間データのディレクトリ、または複数のデータセットを含む親ディレクトリ",
    )
    parser.add_argument(
        "--batch", "-b",
        action="store_true",
        help="バッチモード: 子フォルダ内のデータセットをまとめて変換",
    )
    parser.add_argument(
        "--force", "-f",
        action="store_true",
        help="既存の変換済みファイルも再変換する",
    )
    parser.add_argument(
        "--output", "-o",
        type=Path,
        default=None,
        help="出力npzファイルのパス（単一ディレクトリモード時のみ有効、省略時は input_dir/dataset.npz）",
    )

    args = parser.parse_args()

    input_dir = args.input_dir
    if not input_dir.exists():
        print(f"エラー: 入力ディレクトリが存在しません: {input_dir}", file=sys.stderr)
        sys.exit(1)

    # バッチモード
    if args.batch:
        _, fail = convert_batch(input_dir, skip_existing=not args.force)
        sys.exit(0 if fail == 0 else 1)

    # 単一ディレクトリモード
    if not is_dataset_dir(input_dir):
        # 子ディレクトリを探す
        dataset_dirs = find_dataset_dirs(input_dir)
        if dataset_dirs:
            print(f"ヒント: {len(dataset_dirs)} 個のデータセットが見つかりました。")
            print("       --batch オプションでまとめて変換できます。")
            print()
        print(f"エラー: データセットではありません: {input_dir}", file=sys.stderr)
        sys.exit(1)

    output_file = args.output
    if output_file is None:
        output_file = input_dir / "dataset.npz"

    # 出力ディレクトリが存在しない場合は作成
    output_file.parent.mkdir(parents=True, exist_ok=True)

    try:
        convert_to_npz(input_dir, output_file, verbose=True)
    except Exception as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
