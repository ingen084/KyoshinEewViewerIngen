# KyoshinEewViewer for ingen

<p align="center">
<img src="https://raw.githubusercontent.com/ingen084/KyoshinEewViewerIngen/develop/kevi_icon.png" width="128">
</p>

![downloads-latest](https://img.shields.io/github/downloads/ingen084/KyoshinEewViewerIngen/latest/total)
![downloads-pre](https://img.shields.io/github/downloads-pre/ingen084/KyoshinEewViewerIngen/latest/total)
![downloads-total](https://img.shields.io/github/downloads/ingen084/KyoshinEewViewerIngen/total)

## 概要

**KyoshinEewViewer for ingen** は、日本のリアルタイム地震監視アプリケーションです。強震ネットワークの観測データや気象庁からの緊急地震速報・地震情報を統合し、包括的な地震監視システムを提供します。

### 主な機能

- **リアルタイム地震監視**: 強震モニタと緊急地震速報の表示
- **マルチデータソース対応**: 気象庁XML、DM-D.S.S、強震ネットワークなど複数の情報源を統合
- **地理情報可視化**: 高精度な地図投影による震源・震度分布・津波情報の表示
- **災害危機管理通報**: QZSS（準天頂衛星）による災害・危機管理通報の受信と表示
- **ワークフローシステム**: カスタマイズ可能な自動応答・通知システム
- **音声アラート**: VoiceVoxとの連携による音声通知機能
- **クロスプラットフォーム**: Windows、Linux、macOS対応

![アプリケーション画面](https://github.com/ingen084/KyoshinEewViewerIngen/assets/5910959/4c35a365-fa94-46e5-b84f-9d73a946e2cb)

## システム要件

### 動作環境
- **Windows**: Windows 10 1809以降 (x64/ARM64)
- **Linux**: glibc 2.17以降のディストリビューション (x64/ARM64)
- **macOS**: macOS 10.15以降 (x64/ARM64)

### 開発環境
- **.NET SDK**: 9.0以降
- **IDE**: Visual Studio 2022 / Visual Studio Code / JetBrains Rider
- **Git**: サブモジュール対応

## インストール・使用方法

### リリース版のダウンロード
[GitHub Releases](https://github.com/ingen084/KyoshinEewViewerIngen/releases)から最新版をダウンロードしてください。

### 開発環境のセットアップ

```bash
# リポジトリのクローン（サブモジュール含む）
git clone --recursive https://github.com/ingen084/KyoshinEewViewerIngen.git
cd KyoshinEewViewerIngen

# サブモジュールの初期化（既存リポジトリの場合）
git submodule update --init --recursive

# 依存関係の復元
dotnet restore

# アプリケーションの実行
dotnet run --project src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## ビルド方法

### 開発・デバッグ

```bash
# 開発モードでの実行
dotnet run --project src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# ホットリロード対応の監視実行
dotnet watch run --project src/KyoshinEewViewer/KyoshinEewViewer.csproj

# 全プロジェクトのビルド
dotnet build
```

### 本番用ビルド

```bash
# Windows x64向けシングルファイル配布
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release \
  -r win-x64 \
  -o publish/win-x64 \
  -p:PublishSingleFile=true \
  --self-contained true

# Linux x64向け
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  -o publish/linux-x64 \
  -p:PublishSingleFile=true \
  --self-contained true

# macOS向け
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release \
  -r osx-x64 \
  -o publish/osx-x64 \
  -p:PublishSingleFile=true \
  --self-contained true
```

### テスト実行

```bash
# 全テストの実行
dotnet test

# 特定プロジェクトのテスト
dotnet test tests/KyoshinEewViewer.JmaXmlParser.Tests/
dotnet test tests/KyoshinEewViewer.DCReportParser.Tests/
```

## クレジット・謝辞

### スペシャルサンクス（敬称略）
- **[こんぽ](https://twitter.com/compo031)**: ソフトウェア名称使用許可・開発ノウハウ提供
- **[M-nohira](https://github.com/M-nohira)**: 地理計算アルゴリズム（円描画処理）
- **[JQuake](https://jquake.net/)**: [強震モニタ画像解析手法](https://qiita.com/NoneType1/items/a4d2cf932e20b56ca444)
- **Douglas Peucker Algorithm**: [ライン簡略化アルゴリズム](https://www.codeproject.com/Articles/18936/A-C-Implementation-of-Douglas-Peucker-Line-Appro)

### データ・リソース提供
- **[気象庁](https://www.jma.go.jp/)**: [予報区等GISデータ](https://www.data.jma.go.jp/developer/gis.html)・[JMA2001走時表](https://www.data.jma.go.jp/svd/eqev/data/bulletin/catalog/appendix/trtime/trt_j.html)
- **[Natural Earth](https://www.naturalearthdata.com/)**: 世界地理データ
- **FontAwesome**: フリーアイコンセット
- **源真ゴシック**: フォントファミリー
- **[Avalonia.ThemeManager](https://github.com/wieslawsoltes/Avalonia.ThemeManager)**: テーマ管理機能ベース

### ライセンス

このプロジェクトは [MIT License](LICENSE) の下で配布されています。

---

## リンク

- **公式リポジトリ**: https://github.com/ingen084/KyoshinEewViewerIngen
- **リリース版ダウンロード**: https://github.com/ingen084/KyoshinEewViewerIngen/releases
- **イシュー報告**: https://github.com/ingen084/KyoshinEewViewerIngen/issues
- **開発ドキュメント**: [CLAUDE.md](CLAUDE.md)
