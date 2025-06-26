# CLAUDE.md

## 言語サポート

**日本語対応**: このプロジェクトは日本の防災アプリケーション。開発者・ユーザーとの対話は日本語で行われる。地震・津波・気象などの専門用語は気象庁の用語に準拠すること。

**thinking モード**: 常に「よく考える」 - 複雑な問題に対してthinkingモードを使用し、段階的に問題を分析・解決する。

## プロジェクト概要

**KyoshinEewViewer for ingen** - 日本の防災アプリケーション
- C# .NET 9.0 + Avalonia UI によるクロスプラットフォーム対応
- 気象庁・強震ネットワーク等から地震活動を監視
- リアルタイム緊急地震速報・地震情報を表示

## ビルドコマンド

```bash
# メインプロジェクト
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# デスクトップ版
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## アーキテクチャ

### Series アーキテクチャ
プラグイン式で監視機能を分離したモジュール構成：

- **KyoshinMonitor**: 強震ネットワーク監視・緊急地震速報
- **Earthquake**: 気象庁XML地震情報処理
- **Tsunami**: 津波警報システム
- **Typhoon**: 台風追跡
- **Lightning**: 雷検知
- **Radar**: 気象レーダー
- **Qzss**: 衛星災害危機管理通報

各Series構成（`src/KyoshinEewViewer/Series/[SeriesName]/`）：
- View（AXAML/ViewModel）
- Layer（マップレンダリング）
- Services（データ処理）
- Models（データ構造）
- SettingPages（設定UI）
- Templates（スクリプトテンプレート）
- Workflow（ワークフロー定義）

### 主要技術スタック
- **Avalonia UI**: AXAML、MVVM、クロスプラットフォーム
- **ReactiveUI**: リアクティブプログラミング
- **KyoshinMonitorLib**: 強震モニタ処理
- **FluentAvalonia**: モダンUI
- **Scriban**: テンプレートエンジン
- **ManagedBass**: オーディオ
- **ZLinq**: 高性能LINQ

## プロジェクト構造

### メインプロジェクト
- `KyoshinEewViewer`: メインアプリケーション（Series、UI、サービス）
- `KyoshinEewViewer.Desktop`: デスクトップ版エントリーポイント
- `KyoshinEewViewer.Core`: 共有モデル・テーマ・ユーティリティ
- `KyoshinEewViewer.Map`: 地理レンダリング・マップ投影
- `KyoshinEewViewer.CustomControl`: 専用UIコントロール

### パーサーライブラリ
- `KyoshinEewViewer.JmaXmlParser`: 気象庁XML解析
- `KyoshinEewViewer.DCReportParser`: QZSS災害危機管理通報解析
- `KyoshinEewViewer.CsvSourceGenerator`: CSV辞書のコード生成

### 設定
- `common.props`: 共有MSBuildプロパティ（.NET 9.0、Nullable、等）

## 開発パターン

### UI開発（Avalonia）
- MVVM：`ViewModelBase` 継承のViewModel
- AXAML マークアップ（Avalonia版XAML）
- コンパイル済みバインディング（デフォルト有効）
- FluentAvalonia コンポーネント使用
- **Commandバインディング**: Avaloniaがメソッドを直接Commandとして認識するため、`ICommand`の実装は不要

### データ処理
- Series ベースアーキテクチャ
- ReactiveUI/System.Reactive によるリアクティブストリーム
- スレッドセーフなデータ更新
- マップレイヤーによる地理データ可視化

### テーマシステム
- `IntensityTheme`: 震度表示カラー
- `WindowTheme`: アプリケーションテーマ
- テーマエディター
- System.Text.Json シリアル化

### ワークフローシステム
Scriban テンプレートによるイベント駆動処理：
- **トリガー**: イベント検知条件（地震、緊急地震速報等）
- **アクション**: 応答処理（通知、音声、Webhook等）
- **イベント**: ワークフロー用データ
- **テンプレート**: Scriban による動的コンテンツ生成

## テスト

xUnit フレームワーク使用：
- `KyoshinEewViewer.Tests`: テンプレート系テスト
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML解析検証
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS通報解析検証

**注意**: `tests/` ディレクトリに存在するプロジェクトのみテスト実行

## 重要な注意事項

### Scriban テンプレート
テンプレート編集時は参照資料を確認：
- [言語仕様](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/language.md)
- [組み込み関数](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/builtins.md)

シンプルで分かりやすい実装を心がける。

### ZLinq の使用
LINQ関連エラーの多くは `using ZLinq;` 不足が原因：
- LINQ操作を使用するファイルには必ず追加
- 高性能LINQ操作をコードベース全体で使用

## 開発ガイドライン

### 実装手順
1. **要件明確化**: 仕様が不明確な場合は必ずユーザーに確認
2. **スコープ定義**: UI要件、データ構造、動作を確認
3. **実装計画**: 計画をユーザーに提示し承認
4. **実装**: 確認後のみコーディング開始

**要件推測禁止** - 必ずユーザーと定義：
- UI設計・レイアウト
- データ入出力形式
- 既存システム統合点
- パフォーマンス要件
- エラーハンドリング

### 実装方針
- **DRY原則**: ただし短いコードは可読性を重視
- **積極的な質問**: 提案・反論は遠慮なく
- **TODO残し禁止**: 指示された場合を除く
- **テスト修正**: 実装変更の妥当性を慎重に判断

### ルール追加プロセス
継続対応が必要な指示はCLAUDE.mdへのルール追加を提案し、プロジェクトルールを継続改善。

## 設計ガイドライン

### 通知テンプレート設計
- **詳細ガイド**: `docs/notification-design-guidelines.md`
- **実装例**: `src/KyoshinEewViewer/Series/*/Templates/*Templates.cs`  
- **テストパターン**: `tests/KyoshinEewViewer.Tests/Templates/`

## Gemini検索

`gemini` はGoogle Gemini CLIツールです。Web検索に使用できます。
自信の無い単語や実装は必ずこのコマンドを使用して検索してください。

TaskツールでWeb検索を実行: `gemini -p 'WebSearch: ...'`

```bash
gemini -p "WebSearch: ..."
```
