# CLAUDE.md

## 言語サポート

**日本語対応**: このプロジェクトは日本の防災アプリであり、開発者・ユーザーからの質問や要求は日本語で行われる。
Claude Code は日本語での質問に適切に日本語で回答し、コメントや変数名、ドキュメントにおいても日本語の文脈を理解して対応してください。地震・津波・気象などの専門用語は気象庁の用語に準拠することが重要です。

## プロジェクト概要

**KyoshinEewViewer for ingen** は日本の防災アプリケーション。
C# .NET 9.0 と Avalonia UI でクロスプラットフォーム対応。気象庁・強震ネットワークなど複数データソースから地震活動を監視し、リアルタイム緊急地震速報・地震情報を表示する。

## コマンド

```bash
# メインアプリケーションのビルド
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# デスクトップアプリケーションのビルド
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## アーキテクチャ概要

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

### 主要ライブラリ・コンポーネント
- **Avalonia UI**: AXAML マークアップによるクロスプラットフォーム UI フレームワーク
- **ReactiveUI**: リアクティブプログラミングによる MVVM 実装
- **KyoshinMonitorLib**: 強震モニタのデータ処理ライブラリ
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

## 一般的な開発問題

### LINQ関連コンパイルエラー
LINQ関連のコンパイルエラー（列挙メソッド不足やパフォーマンス関連問題）が発生した場合、`using ZLinq;` ディレクティブの不足が原因の可能性が高い。ZLinq は高性能LINQ操作を提供し、コードベース全体で使用されている。

**解決方法**: LINQ操作を使用するファイルの先頭に `using ZLinq;` を追加。

### エラーメッセージ例：
- 拡張メソッド列挙エラー
- パフォーマンス関連LINQ警告
- LINQメソッド実装不足

## Claude Code ワークフローガイドライン

### 最重要事項

必ず以下の手順に従う：

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

### 実装方針

- クラスなどの作成はDRYを原則とするが、短く簡潔なコードに関してはその限りではない。
  - ブラックボックス化によりかえって見通しが悪くなる場合はそのまま記述することを検討する。
- ユーザーからの指示をすべて鵜呑みにせず、反論や提案がある場合はしっかり質問し直すこと。
  - ユーザーとの合意が取れて初めて洗練したプロダクトが実現される。
- ユーザーから指示されている場合を除き、TODO は残さない。

### 新しいルールの追加プロセス

ユーザーから常に対応が必要だと思われる指示を受けたか変更が加えられた場合、これをルールにするか尋ね、同意が得られた場合は CLAUDE.md にルールを追加し、以降は標準ルールとして常に適用。
プロジェクトのルール改善を継続的に行う。
