# CLAUDE.md

Claude Code (claude.ai/code) がこのリポジトリで作業する際のガイダンスです。

## 言語サポート

**日本語対応**: このプロジェクトは日本の地震監視アプリであり、開発者・ユーザーからの質問や要求は日本語で行われます。Claude Code は日本語での質問に適切に日本語で回答し、コメントや変数名、ドキュメントにおいても日本語の文脈を理解して対応してください。地震・津波・気象などの専門用語は気象庁の用語に準拠することが重要です。

## プロジェクト概要

**KyoshinEewViewer for ingen** は日本のリアルタイム地震監視アプリケーションです。C# .NET 9.0 と Avalonia UI でクロスプラットフォーム対応。気象庁・強震ネットワークなど複数データソースから地震活動を監視し、リアルタイム緊急地震速報・地震情報を表示します。

## ビルド・開発コマンド

### 前提条件
- .NET SDK 9.0 以上
- Git（サブモジュール対応）

### 共通コマンド

```bash
# メインアプリケーションのビルド
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# デスクトップアプリケーションのビルド
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# 開発モードでの実行
dotnet run --project src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj

# 変更監視での実行
dotnet watch run --project src/KyoshinEewViewer/KyoshinEewViewer.csproj

# 単体テスト実行
dotnet test tests/KyoshinEewViewer.JmaXmlParser.Tests/
dotnet test tests/KyoshinEewViewer.DCReportParser.Tests/
dotnet test  # 全テスト実行

# 本番用パブリッシュ（Windows x64 例）
dotnet publish src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj \
  -c Release \
  -r win-x64 \
  -o publish \
  -p:PublishSingleFile=true \
  --self-contained true
```

### VS Code 連携
- **F5**: メインアプリケーションのデバッグ
- **Ctrl+Shift+P** → "Tasks: Run Task" → "build", "publish", "watch"

## アーキテクチャ概要

### マルチプラットフォーム対応
- **Desktop**: Windows, Linux, macOS（主要ターゲット）
- **Android**: モバイルアプリ版
- **Browser**: WebAssembly版
- **Core**: 全プラットフォーム共通ロジック

### モジュラーSeries アーキテクチャ
プラグイン式Series システムで監視機能を分離したモジュール構成：

- **KyoshinMonitor**: 強震ネットワークからのリアルタイム地震監視
- **Earthquake**: 気象庁XML配信からの地震情報処理
- **Tsunami**: 津波警報システム
- **Typhoon**: 台風追跡
- **Lightning**: 雷検知
- **Radar**: 気象レーダー統合
- **QZSS**: 衛星災害危機管理通報

各Seriesは `src/KyoshinEewViewer/Series/[SeriesName]/` に配置され以下を含む：
- View（AXAML/ViewModel）
- Layer（マップレンダリング）
- Services（データ処理）
- Models（データ構造）
- SettingPages（設定UI）

### データ処理パイプライン
1. **リアルタイム取得**: 複数データソース（気象庁、DM-D.S.S、強震ネットワーク）
2. **XML解析**: `KyoshinEewViewer.JmaXmlParser` による気象庁XML形式処理
3. **マップレンダリング**: カスタム投影法・地理データ処理
4. **ワークフローシステム**: Scriban テンプレートによる自動応答
5. **通知システム**: クロスプラットフォーム通知・音声アラート

### 主要ライブラリ・コンポーネント
- **Avalonia UI**: AXAML マークアップによるクロスプラットフォーム UI フレームワーク
- **ReactiveUI**: リアクティブプログラミングによる MVVM 実装
- **KyoshinMonitorLib**: 地震データ処理コア
- **FluentAvalonia**: モダン UI コンポーネント
- **Scriban**: ワークフロー用テンプレートエンジン
- **ManagedBass**: オーディオ再生
- **ZLinq**: 高性能 LINQ 操作

## プロジェクト構造

### コアプロジェクト
- `KyoshinEewViewer.Core`: 共有モデル、テーマ、ユーティリティ
- `KyoshinEewViewer`: メインアプリケーションロジック・UI
- `KyoshinEewViewer.Desktop`: デスクトップ固有実装・エントリーポイント
- `KyoshinEewViewer.Map`: 地理レンダリング・マップ投影
- `KyoshinEewViewer.CustomControl`: 専用UIコントロール（震度表示、マップコントロール）

### 解析ライブラリ
- `KyoshinEewViewer.JmaXmlParser`: 気象庁XML解析
- `KyoshinEewViewer.DCReportParser`: QZSS災害危機管理通報解析
- `KyoshinEewViewer.CsvSourceGenerator`: CSVベースデータ辞書のコード生成

### 設定ファイル
- `common.props`: 共有MSBuildプロパティ（.NET 9.0、Nullable有効、AOT設定）
- `workflows.json`: ユーザーワークフロー設定（メイン設定とは分離）
- `config.json`: メインアプリケーション設定

## 開発パターン

### UI開発（Avalonia/AXAML）
- `ViewModelBase` を継承したViewModel による MVVM パターン
- UI マークアップは AXAML ファイル（Avalonia の XAML バリアント）
- パフォーマンス向上のためコンパイル済みバインディングがデフォルト有効
- モダンUI要素には FluentAvalonia コンポーネントを使用

### リアルタイムデータ処理
- データタイプ別のSeries ベースアーキテクチャ
- ReactiveUI/System.Reactive を使用したリアクティブストリーム
- 適切な同期によるスレッドセーフなデータ更新
- 地理データ可視化のためのマップレイヤー

### 設定・テーマ
- `IntensityTheme`: 震度表示用カラースキーム
- `WindowTheme`: アプリケーション視覚テーマ
- カスタマイズ用テーマエディターウィンドウ
- ソースジェネレーター付き System.Text.Json によるシリアル化

### ワークフローシステム
Scriban テンプレートを使用した高度なワークフローシステム：
- **トリガー**: ワークフロー開始条件（地震検知、緊急地震速報受信）
- **アクション**: トリガーへの応答（通知、音声、Webhook）
- **イベント**: テンプレート処理用ワークフローデータ
- **テンプレート**: 動的コンテンツ用 Scriban ベーステキスト処理

## テスト

### テストプロジェクト
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML解析検証
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS通報解析検証

### テストパターン
- 標準命名規則による xUnit フレームワーク
- テストプロジェクトディレクトリ内のテストデータ
- 外部依存関係用モックサービス
- **注意**: 特定機能・コンポーネントにテストプロジェクトが存在しない場合、テストは不要
- `tests/` ディレクトリに明示的なテストプロジェクトが存在する場合のみテスト実行

## Git サブモジュール

気象庁コード定義用 `jma-code-dictionary` サブモジュールを含む：
```bash
git submodule update --init --recursive
```

## クロスプラットフォーム考慮事項

### ネイティブライブラリ
- オーディオライブラリ（ManagedBass）は `src/KyoshinEewViewer.Desktop/libs/` にプラットフォーム固有配置
- Linux固有コードは `LINUX` 条件コンパイル使用
- `common.props` でビルド時プラットフォーム検出設定

### ファイルパス・リソース
- クロスプラットフォームパス処理には `Path.Combine()` 使用
- `Assets/` ディレクトリ内の埋め込みリソース
- Desktop プロジェクトでプラットフォーム固有リソース処理

## 一般的な開発問題

### LINQ関連コンパイルエラー
LINQ関連のコンパイルエラー（列挙メソッド不足やパフォーマンス関連問題）が発生した場合、`using ZLinq;` ディレクティブの不足が原因の可能性が高い。ZLinq は高性能LINQ操作を提供し、コードベース全体で使用されている。

**解決方法**: LINQ操作を使用するファイルの先頭に `using ZLinq;` を追加。

### エラーメッセージ例：
- 拡張メソッド列挙エラー
- パフォーマンス関連LINQ警告
- LINQメソッド実装不足

## Claude Code ワークフローガイドライン

### 機能実装プロセス

**重要**: 新機能実装時は必ず以下の手順に従う：

1. **要件明確化**: 機能仕様が不明確な場合は**必ずユーザーに確認**してから実装開始
2. **スコープ定義**: 正確な範囲、UI要件、データ構造、期待される動作を確認
3. **実装計画**: 明確な実装計画をユーザーに提示し承認を得る
4. **実装**: ユーザーの明示的確認後にのみコーディング開始

**要件を推測しない**、不完全な仕様での機能実装は禁止。必ずユーザーと以下を定義：
- UI設計・レイアウト
- データ入出力形式
- 既存システムとの統合点
- パフォーマンス要件
- エラーハンドリングアプローチ

### ドキュメント更新

Claude Code による重要な作業（新機能追加、アーキテクチャ変更、新ライブラリ導入、ビルド設定変更）実行時は CLAUDE.md の以下領域の更新を検討：

1. **新コマンド・ビルド手順**の追加
2. **新プロジェクト構造・アーキテクチャパターン**の導入
3. **新依存関係・ライブラリ**の追加
4. **開発パターン・コーディング規約**の変更
5. **テスト手順・デプロイメント方法**の変更

更新が必要な場合は関連セクションに情報を追加し、将来のClaude Codeセッションの効率化を図る。特に日本語技術用語・ドメイン固有プロセスの文書化が重要。