# KyoshinEewViewerIngen
Kyoshin Eew Viewer for ingen

## クレジット/スペシャルサンクス (敬称略)

- [こんぽ](https://twitter.com/compo031)
  - 観測点情報の提供
- [M-nohira](https://github.com/M-nohira)
  - 距離と中心座標から円を描画するアルゴリズムの提供
- [ResourceBinding](https://stackoverflow.com/questions/20564862/binding-to-resource-key-wpf)
- [OpenStreetMap](https://openstreetmap.org)
  - 地図データ

## ビルド方法

### 必要環境

- Windows10
- .NET Core SDK 3.0.100 以上
- (スクリプト実行時のみ)CドライブにインストールしたVisual Studio 2019

### 追加で必要なファイル

`/src/KyoshinEewViewer/Resources/ShindoObsPoints.mpk.lz4`  
こんぽ氏から頂いたものを加工したものですが、再配布は許可されていませんので [KyoshinShindoPlaceEditor](https://github.com/ingen084/KyoshinShindoPlaceEditor) を使用して作成して頂く必要があります。

### お手軽ビルド

1. `publish.bat` を実行します。
2. なんやかんやあって `tmp/KyoshinEewViewer.exe` が生成されます。

### 注意点

[Wrap](https://github.com/dgiagio/warp) を使用しています。  
これはファイルの更新日時を見て一時ファイルを更新しているため、状況によっては最新バージョンが適用されない可能性があります。