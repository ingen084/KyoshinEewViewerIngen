# KyoshinEewViewer for ingen テーマ作成マニュアル

## はじめに

今のところ画面上でテーマを確認･編集する方法はありません。  
json のファイル形式、カラーコード(`#FFFFFF`等)の知識が前提となります。  
画面上からのテーマ編集機能が欲しい方は作者までご相談ください。優先度を上げて対応します。

初期搭載したいテーマができましたらご連絡ください。良い感じだったら採用します。  
ただ初期搭載テーマに移植するのがかなり面倒くさいので PR 直接提出も歓迎します。

## ファイル構成

テーマは json 形式で記述します。  
アプリは**起動時に**実行ファイル(`KyoshinEewViewer.exe`)と同じフォルダ上の `Themes`/`IntensityThemes` フォルダ内の json ファイルを読み込もうとします。

ウィンドウテーマは `Themes` から、震度アイコンテーマは `IntensityThemes` から読み込まれます。  
このようにファイルを配置するだけですので、良い感じのテーマができたら配布してみてもいいかもしれません。  
ただし将来のバージョンの互換を保つ約束はできないので動作確認済みのバージョンも追記しておくと良いでしょう。

## ファイルフォーマット

### カラーコード

`#RRGGBB` もしくは `#AARRGGBB` で指定してください。  
透明度は `FF` は不透明で、 `00` が透明です。

### ウィンドウテーマ

以下の形式です。

|プロパティ名|型|解説|
|:--|:--|:--|
|Name|string|テーマ名 設定ファイル上でも使用されます|
|TitleBackgroundColor|Color|タイトルバーの色 左ペインの配色(Windows のみタイトルバーの色も変化します)|
|IsDark|bool|ダークテーマか UIの基本色が変化します|
||||
|OverseasLandColor|Color|地図配色 海外地形(ボーダーは設定不可)|
|LandColor|Color|地図配色 地形|
|LandStrokeThickness|float|地図配色 海岸線の太さ 0 にすることで軽量化できる|
|PrefStrokeColor|Color|地図配色 都道府県境界線|
|PrefStrokeThickness|float|地図配色 都道府県境界線の太さ|
|AreaStrokeColor|Color|地図配色 地域境界線|
|AreaStrokeThickness|float|地図配色 地域境界線の太さ|
||||
|MainBackgroundColor|Color|メイン背景色|
|ForegroundColor|Color|メイン文字色|
|SubForegroundColor|Color|サブ文字色(補足等)|
|EmphasisForegroundColor|Color|強調文字(現状では強震モニタリプレイ時の時刻色)|
||||
|DockBackgroundColor|Color|ドック(要素ウィンドウ)背景色|
|DockTitleBackgroundColor|Color|ドック(要素ウィンドウ)タイトル部分背景色|
|DockWarningBackgroundColor|Color|ドックエラー･警告配色背景色|
|DockWarningTitleBackgroundColor|Color|ドックエラー･警告配色タイトル部分背景色|
||||
|WarningForegroundColor|Color|エラー･警告文字色|
|WarningSubForegroundColor|Color|エラー･警告サブ文字色|
|WarningBackgroundColor|Color|エラー･警告背景色|
||||
|TsunamiForecastColor|Color|津波予報色|
|TsunamiForecastForegroundColor|Color|津波予報文字色|
|TsunamiAdvisoryColor|Color|津波注意報色|
|TsunamiAdvisoryForegroundColor|Color|津波注意報文字色|
|TsunamiWarningColor|Color|津波警報色|
|TsunamiWarningForegroundColor|Color|津波警報文字色|
|TsunamiMajorWarningColor|Color|津波大津波警報色|
|TsunamiMajorWarningForegroundColor|Color|津波大津波警報文字色|
||||
|EarthquakeHypocenterBorderColor|Color|震央アイコンボーダー色(地震情報)|
|EarthquakeHypocenterColor|Color|震央アイコン塗りつぶし色(地震情報)|
||||
|EewForecastHypocenterBorderColor|Color|震央アイコンボーダー色(緊急地震速報 予報)|
|EewForecastHypocenterColor|Color|震央アイコン塗りつぶし色(緊急地震速報 予報)|
|EewWarningHypocenterBorderColor|Color|震央アイコンボーダー色(緊急地震速報 警報)|
|EewWarningHypocenterColor|Color|震央アイコン中央色(緊急地震速報 警報)|
||||
|IsEewHypocenterBlinkAnimation|bool|緊急地震速報震央アイコンの点滅アニメーションを有効にするか|
||||
|EewForecastPWaveColor|Color|緊急地震速報(予報)P波色|
|EewForecastSWaveColor|Color|緊急地震速報(予報)S波色|
|IsEewForecastSWaveGradient|bool|緊急地震速報(予報)のS波色をグラデーションにするか|
||||
|EewWarningPWaveColor|Color|緊急地震速報(警報)P波色|
|EewWarningSWaveColor|Color|緊急地震速報(警報)S波色|
|IsEewWarningSWaveGradient|bool|緊急地震速報(警報)のS波色をグラデーションにするか|

### 震度アイコンテーマ

|プロパティ名|型|解説|
|:--|:--|:--|
|Name|string|テーマ名 設定ファイル上でも使用されます|
|IntensityColors|object|震度階級(後述)|
|LpgmIntensityColors|object|長周期地震動階級(後述)|
|BorderWidthMultiply|float|縁の太さの割合 (参考値 Standard/Quarog:`0.125` JMA/Vivid:`0.05`)|

#### 震度階級

震度をキーに `Foreground`/`Background`/`Border` を設定します。  
実装例を参考にしてください。

- `Int0`~`Int4`/`Int7`
- `Int5Upper`/`Int5Lower`
- `Int6Upper`/`Int6Lower`

#### 長周期地震動階級

震度をキーに `Foreground`/`Background`/`Border` を設定します。  
実装例を参考にしてください。

- `LpgmInt0`~`LpgmInt4`
- `Unknown`
- `Error`

#### 実装例

[Kiwi Monitor カラースキーム 第3版](https://kiwimonitor.amebaownd.com/posts/36819100/) を実装してみた例です(縁の色は適当)。

```json
{
    "Name": "KiwiV3",
    "IntensityColors": {
        "Unknown": {
            "Foreground": "#E6000000",
            "Background": "#808080",
            "Border": "#999999"
        },
        "Error": {
            "Foreground": "#b30f20",
            "Background": "#ffff6c",
            "Border": "#FFFF52"
        },
        "Int0": {
            "Foreground": "#E6FFFFFF",
            "Background": "#808080",
            "Border": "#999999"
        },
        "Int1": {
            "Foreground": "#E6FFFFFF",
            "Background": "#3C5A82",
            "Border": "#29405E"
        },
        "Int2": {
            "Foreground": "#E6FFFFFF",
            "Background": "#1E82E6",
            "Border": "#135EA9"
        },
        "Int3": {
            "Foreground": "#E6000000",
            "Background": "#78E6DC",
            "Border": "#56A9A1"
        },
        "Int4": {
            "Foreground": "#E6000000",
            "Background": "#FFFF96",
            "Border": "#BCBC6D"
        },
        "Int5Lower": {
            "Foreground": "#E6000000",
            "Background": "#FFD200",
            "Border": "#BC9A00"
        },
        "Int5Upper": {
            "Foreground": "#E6000000",
            "Background": "#FF9600",
            "Border": "#BC6D00"
        },
        "Int6Lower": {
            "Foreground": "#E6FFFFFF",
            "Background": "#F03200",
            "Border": "#B02200"
        },
        "Int6Upper": {
            "Foreground": "#E6FFFFFF",
            "Background": "#BE0000",
            "Border": "#8B0000"
        },
        "Int7": {
            "Foreground": "#E6FFFFFF",
            "Background": "#8C0028",
            "Border": "#65001A"
        }
    },
    "LpgmIntensityColors": {
        "Unknown": {
            "Foreground": "#000000",
            "Background": "#808080",
            "Border": "#999999"
        },
        "Error": {
            "Foreground": "#b30f20",
            "Background": "#ffff6c",
            "Border": "#FFFF52"
        },
        "LpgmInt0": {
            "Foreground": "#E6FFFFFF",
            "Background": "#808080",
            "Border": "#999999"
        },
        "LpgmInt1": {
            "Foreground": "#E6000000",
            "Background": "#78E6DC",
            "Border": "#56A9A1"
        },
        "LpgmInt2": {
            "Foreground": "#E6000000",
            "Background": "#FFD200",
            "Border": "#BC9A00"
        },
        "LpgmInt3": {
            "Foreground": "#E6FFFFFF",
            "Background": "#F03200",
            "Border": "#B02200"
        },
        "LpgmInt4": {
            "Foreground": "#E6FFFFFF",
            "Background": "#BE0000",
            "Border": "#8B0000"
        }
    },
    "BorderWidthMultiply": 0.125
}
```

## テーマがリストに出てこない･反映されない場合

json フォーマットエラーなどで読み込めない場合テーマの一覧に表示されません。  
カラーコードの書式エラーの場合デフォルトテーマの色が使用されます。

読み込まれていない場合はフォーマットが合っているかどうかを見直してみてください。  
また、デフォルトのテーマと同じ名前に設定した場合はメニューからの反映はできますが再起動後に追加されたテーマではなくデフォルトの方のテーマが選択されることになります。
