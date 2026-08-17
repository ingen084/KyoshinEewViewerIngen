# CLAUDE.md

## プロジェクト概要

**KyoshinEewViewer for ingen** — 日本の防災情報アプリケーション。

- C# .NET 10.0 + Avalonia UI によるクロスプラットフォーム対応
- 強震モニタ・気象庁(JMA)の地震情報を監視し、緊急地震速報や地震情報をリアルタイム表示する

## 言語ポリシー

防災アプリケーションという性質上、ユーザーに届く情報はすべて日本語で書く。

- **UIテキスト・ダイアログ・エラーメッセージ**: 日本語
- **ログメッセージ**: 日本語
- **コード内コメント**: 日本語
- **用語**: 地震・津波・気象用語は気象庁(JMA)の標準に従う
- 技術ドキュメントやコード構造の説明は英語でもよいが、実装内容の説明は日本語を優先する

## ビルド

```bash
# メインプロジェクト
dotnet build src/KyoshinEewViewer/KyoshinEewViewer.csproj

# デスクトップ版
dotnet build src/KyoshinEewViewer.Desktop/KyoshinEewViewer.Desktop.csproj
```

## アーキテクチャ

### Series アーキテクチャ

監視機能ごとにモジュール分割されたプラグイン型の構成。

| Series | 役割 |
|---|---|
| KyoshinMonitor | 強震モニタの監視・緊急地震速報 |
| Earthquake | JMA XML 地震情報の処理 |
| Tsunami | 津波警報 |
| Typhoon | 台風追跡 |
| Lightning | 落雷検知 |
| Radar | 気象レーダー |
| Qzss | 衛星災害危機管理通報(災危通報) |

各 Series の構成 (`src/KyoshinEewViewer/Series/[SeriesName]/`): View (AXAML/ViewModel)、Layer (地図描画)、Services (データ処理)、Models、SettingPages (設定UI)、Templates (通知テンプレート)、Workflow。

### プロジェクト構成

- `KyoshinEewViewer`: メインアプリ (Series、UI、サービス)
- `KyoshinEewViewer.Desktop`: デスクトップ版エントリポイント
- `KyoshinEewViewer.Core`: 共有モデル・テーマ・ユーティリティ
- `KyoshinEewViewer.Map`: 地図描画・投影
- `KyoshinEewViewer.CustomControl`: カスタムUIコントロール
- `KyoshinEewViewer.JmaXmlParser`: JMA XML パーサー
- `KyoshinEewViewer.DCReportParser`: QZSS 災危通報パーサー
- `KyoshinEewViewer.CsvSourceGenerator`: CSV辞書のコード生成
- `common.props`: 共有 MSBuild プロパティ (.NET 10.0、Nullable など)

### 技術スタック

Avalonia UI (AXAML / MVVM / コンパイル済みバインディング)、CommunityToolkit.Mvvm、R3、System.Reactive、Microsoft.Extensions.DependencyInjection / Logging、KyoshinMonitorLib、FluentAvalonia、Scriban (テンプレートエンジン)、ManagedBass (音声)。

ReactiveUI / Splat / DynamicData は使わない (移行済み)。対応は以下のとおり。

| 旧 (ReactiveUI / Splat) | 現行 |
|---|---|
| `ReactiveObject` / `RaiseAndSetIfChanged` | `ObservableObject` / `SetProperty` (CommunityToolkit.Mvvm) |
| `WhenAnyValue(x => x.Foo)` | `ObservePropertyChanged(x => x.Foo)` (R3) |
| `WhenAnyValue(x => x.A.B)` (入れ子) | `ObservePropertyChanged(x => x.A, x => x.B)` |
| `WhenAnyValue(a, b, (x, y) => ...)` (合成) | `Observable.CombineLatest(...)` |
| `ToProperty` / `ObservableAsPropertyHelper` | 通常プロパティ + `Subscribe` で代入 |
| `MessageBus.Current.Listen<T>()` / `SendMessage` | `StrongReferenceMessenger.Default.Register<T>` / `Send` |
| `ReactiveCommand.Create` / `CreateFromTask` | `RelayCommand` / `AsyncRelayCommand` |
| `Locator.Current` / `SplatRegistrations` | `ServiceLocator.Current` / `IServiceCollection` |
| `ILogManager` / `LogHost` | `ILogger<T>` / `AppLog` |

## 開発パターン

### UI開発 (Avalonia)

- ViewModel は `ViewModelBase` を継承する
- **コマンドバインディング**: Avalonia はメソッドを直接コマンドとして認識するため `ICommand` 実装は不要。ただし Avalonia 12 以降、コンパイル済みバインディングが解決できるのは引数なし、または `object` 1個のメソッドのみ (それ以外は AVLN2000)。`CommandParameter` を受け取るメソッドは `object?` で受けてガード節でキャストする (`if (parameter is not Foo foo) return;`)。C# からも型付き引数で呼ぶメソッドは、型付き版に加えて XAML 用の引数なしオーバーロードを用意する
- **StringFormat バインディング**: 表示専用で数値や日時を `StringFormat` バインドする場合 (`Run`/`TextBlock`/`Label` 含む) は必ず `Mode=OneWay` を明示する。指定しないと逆変換が試みられ、単位付き文字列 (例: `"000.1 km/h"`) で first-chance の `FormatException` 等が発生する。それでも解消しない場合は ViewModel 側で整形済み文字列プロパティを公開する
- **Markdown 表示**: 必ず `Controls/MarkdownViewer.cs` (`MarkdownViewer`) 経由で描画する。LiveMarkdown.Avalonia 2.2.0 のリンククリック不具合への回避策が入っているため、素の `MarkdownRenderer` は使わない

#### 条件付きスタイル

bool プロパティに応じたスタイル切り替えには、コンバーターではなく `Classes.` 構文を使う:

```xml
<Button>
    <Button.Styles>
        <Style Selector="ui|FASymbolIcon.muted">
            <Setter Property="Foreground" Value="{DynamicResource EmphasisForegroundColor}" />
        </Style>
    </Button.Styles>
    <ui:FASymbolIcon Classes.muted="{Binding IsMuted}" />
</Button>
```

- 複数コントロール・複数ファイルで再利用するスタイルは `UserControl.Styles` / `Window.Styles` や共有リソースディクショナリへ抽出する。アプリ全体で使うものはテーマファイルや `App.axaml` を検討する
- コンバーターを使うのは、複数箇所で再利用する場合や、単純な条件スタイルでは表現できない複雑な変換の場合のみ

### データ処理

- R3 / System.Reactive によるリアクティブストリーム
- データ更新はスレッドセーフに行う
- **地図レイヤー描画**: CPU側でビットマップに事前ラスタライズしてキャッシュするのではなく、シェーダー (`SKRuntimeEffect`/SKSL) で毎フレーム、メッシュデータから直接画面出力へ変換する方式を優先する。テクスチャとして GPU にデータを渡す場合は「画像キャッシュ」ではなくデータ格納形式として扱う。ズームに応じた LOD (一次メッシュ粒度の切り替えなど) も選択肢に入れる

### 命名規則

- **P2P地震情報**: P2Pネットワーククライアント本体と、独立した HTTP/WebSocket の「P2P地震情報 JSON API」の2系統が存在する。データはほぼ同一のため、汎用・共通部分は `P2PQuake`、API固有部分は `P2PQuakeJsonApi` を使う (既存コードでは後者に `P2pQuakeApi` を使用)

### サンドボックスアプリ

PiDASPlusGraph などのおまけアプリは、`KyoshinEewViewer.Core` の共有コアモデル (例: `WindowTheme`) の変更を必要としてはならない (本体アプリ全体に影響するため)。既存の `DynamicResource` 公開色 (例: `SubForegroundColor`) を再利用するか、サンドボックスプロジェクト内で値を定義する。

### テーマシステム

- `IntensityTheme`: 震度表示色 / `WindowTheme`: アプリテーマ
- System.Text.Json でシリアライズ

### ワークフローシステム

Scriban テンプレートによるイベント駆動処理。**Trigger** (地震・緊急地震速報などの検知条件) → **Action** (通知・音声・Webhook などの応答処理)。Event がワークフローのデータを運び、Template が Scriban で動的コンテンツを生成する。

## ロギング

Microsoft.Extensions.Logging (MEL) を直接使う。独自のログ拡張メソッドは持たない。

### 実装パターン

```csharp
// ILogger<T> の直接DI (推奨)
public class SampleService : ObservableObject, IDisposable
{
    private ILogger Logger { get; }

    public SampleService(ILogger<SampleService> logger)
    {
        Logger = logger;
    }
}

// DIでロガーを受け取れない静的クラスなどでは AppLog を使う
AppLog.Default.LogError(ex, "処理に失敗しました");
// DI管理外のオブジェクトを手動で生成する場合は型付きロガーを作る
var child = new SomeHelper(AppLog.Create<SomeHelper>());
```

`AppLog` は `KyoshinEewViewer.Core` にある。`LoggingAdapter.Setup()` 前は何も出力しない `NullLogger` として振る舞う。

### ログメッセージの規則

- 日本語で書く
- **必ず構造化ログにする**。文字列補間 (`$"..."`) は使わず、名前付きプレースホルダと引数で渡す

  ```csharp
  // 良い例
  Logger.LogInformation("電文を取得しました: {EventId} ({Elapsed:0.000}ms)", eventId, sw.Elapsed.TotalMilliseconds);

  // 悪い例 — 構造化されず、値が本文に埋没する
  Logger.LogInformation($"電文を取得しました: {eventId} ({sw.Elapsed.TotalMilliseconds:0.000}ms)");
  ```

- プレースホルダ名は PascalCase。同一メッセージ内で重複させない (`{Name}` / `{Name2}`)
- 書式指定子はプレースホルダ側に置く (`{Time:yyyy/MM/dd HH:mm:ss}`)
- **外部由来の文字列 (ユーザー入力・JSON・電文本文など) を本文に直接連結しない**。`{}` を含むとテンプレートとして解釈され壊れるため、必ず引数で渡す

  ```csharp
  // 良い例
  Logger.LogDebug("受信しました: {Payload}", JsonSerializer.Serialize(message));
  // 悪い例 — JSON の {} がプレースホルダとして解釈される
  Logger.LogDebug("受信しました: " + JsonSerializer.Serialize(message));
  ```

- 例外は `Logger.LogError(ex, "メッセージ")` のように第1引数で渡す
- **Error レベルは Sentry で開発者に送信される**。バグ検知や重要な問題の追跡が特に必要な場合を除き、Warning を使う

## UI操作パターン

### サブウィンドウ

設定ウィンドウなどは `ISubWindowsService` 経由で表示する:

```csharp
var subWindowService = ServiceLocator.Current.GetService<ISubWindowsService>();
subWindowService?.ShowSettingWindow();
```

### ダイアログ

確認・エラーダイアログには `FluentAvalonia.UI.Controls.FAContentDialog` を使う:

```csharp
var result = await new FAContentDialog
{
    Title = "確認",
    Content = "この操作を実行しますか？",
    PrimaryButtonText = "はい",
    SecondaryButtonText = "いいえ",
    DefaultButton = FAContentDialogButton.Secondary
}.ShowAsync(this);

if (result == FAContentDialogResult.Primary)
{
    // 処理を実行
}
```

### トップレベルコントロール

ファイル選択やダイアログ表示の親ウィンドウには `KyoshinEewViewerApp.TopLevelControl` を使う:

```csharp
if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;
var files = await tlc.StorageProvider.OpenFilePickerAsync(options);
```

## テスト

xUnit を使用。`tests/` ディレクトリに存在するプロジェクトのみテストを実行する。

- `KyoshinEewViewer.Tests`: テンプレートシステム
- `KyoshinEewViewer.JmaXmlParser.Tests`: XML パース検証
- `KyoshinEewViewer.DCReportParser.Tests`: QZSS 災危通報パース検証

### テスト方針

- クラスの中核機能・ビジネスロジックに焦点を当て、公開APIの振る舞いと期待される結果を検証する
- 関連するシナリオはまとめて包括的なテストメソッドにする。類似シナリオはデータ配列やループで1メソッドに集約してよい
- テスト名は日本語の `DisplayName` でシナリオが分かるように書く

  ```csharp
  [Fact(DisplayName = "ビルダーパターンのメソッドチェーニングが正常に動作する")]
  ```

- テスト対象にしないもの: 単純なプロパティの設定/取得、`this` を返すだけのメソッドチェーン、単純な初期状態、インフラ実装詳細 (ManualResetEventSlim、内部タイマーなど)、定数定義、enum の存在確認
- 実装変更に伴うテスト修正は、その変更が妥当かをよく確認してから行う

### テストデータのURL規則

テスト実行中に本番サービスへ誤ってリクエストが飛ぶことを防ぐため、**テストデータに実在の本番URL・エンドポイントを使わない**。`api.dmdata.jp` / `ws.api.dmdata.jp` / `ws-tokyo.api.dmdata.jp` などの実ホスト名は禁止。

代わりに、名前解決できない改変文字列を使う:

- 改変URL: `wsdmdatajp`、`customapidmdatajp` など
- 改変エンドポイント: `tokyodmdatajp`、`osakadmdatajp` など
- ドメインが無関係なら `examplecom`、エラーテストには `invalid-endpoint` / `test-endpoint`

```csharp
// 良い例 — 実リクエストが発生しない改変URL
Url = "wss://wsdmdatajp/v2/socket"

// 悪い例 — 本番URL
Url = "wss://ws.api.dmdata.jp/v2/socket"
```

## 実装の進め方

### 要件の確認

仕様が不明確なときは推測で進めず、ユーザーと確認してから実装する。特に以下は事前にすり合わせる: UIデザイン・レイアウト、データ入出力形式、既存システムとの接続点、パフォーマンス要件、エラーハンドリング。実装方針はユーザーに提示し、承認を得てからコーディングを開始する。

### 実装ポリシー

- 依頼された範囲・意図されたスコープで実装する。頼まれていない機能追加やリファクタリングは行わない
- 過剰な抽象化レイヤーを作らず、必要最小限の設計で実装する
- DRY 原則に従う。ただし短いコードでは可読性を優先してよい
- 早期リターンを使う場合は不要な `else` を避け、ガード節で可読性を上げる
- 「新規」「追加」「修正」「削除」のような、その場でしか意味を持たない一時的なコメントは残さない (開発中に書いた場合は仕上げ時に削除する)
- TODO は指示がない限り残さない
- 疑問や改善案があれば遠慮なく提案・指摘する

### ルールの追加

他の場面でも役立ちそうな知見が得られたら、CLAUDE.md への追記を提案してプロジェクトルールを継続的に改善する。

## 参考資料

- **Scriban テンプレート**: 編集時は [言語仕様](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/language.md) と [組み込み関数](https://raw.githubusercontent.com/scriban/scriban/refs/heads/master/doc/builtins.md) を参照する。シンプルで分かりやすい実装を心がける
- **通知テンプレートの設計**: `docs/notification-design-guidelines.md` (実装例: `src/KyoshinEewViewer/Series/*/Templates/*Templates.cs`、テスト: `tests/KyoshinEewViewer.Tests/Templates/`)
