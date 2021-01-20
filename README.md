# KyoshinEewViewerIngen
Kyoshin Eew Viewer for ingen

## クレジット/スペシャルサンクス (敬称略)

- [こんぽ](https://twitter.com/compo031)
  - ソフトウェア名称の使用許可
  - 制作にあたってのノウハウなど
- [M-nohira](https://github.com/M-nohira)
  - 距離と中心座標から円を描画するアルゴリズム( `RenderObjects/EllipseRenderObject.cs` )
- [ResourceBinding](https://stackoverflow.com/questions/20564862/binding-to-resource-key-wpf)
- [Douglas Peucker algorithm](https://www.codeproject.com/Articles/18936/A-C-Implementation-of-Douglas-Peucker-Line-Appro)
- [国土数値情報 行政区域](https://nlftp.mlit.go.jp/ksj/index.html) / [Natural Earth](https://www.naturalearthdata.com/)
  - TopoJsonにし、MessagePack+LZ4加工して使用
- FontAwesome 5 Free
- 源真ゴシック
- [JMA2001走時表](https://www.data.jma.go.jp/svd/eqev/data/bulletin/catalog/appendix/trtime/trt_j.html) (C) JMA
  - 深さを10km刻み、時間を1000倍し整数にしたものをMessagePack+LZ4に加工して使用

## ビルド方法

### 必要環境

- Windows10
- .NET Core SDK 3.1.100 以上
- (スクリプト実行時のみ)CドライブにインストールしたVisual Studio 2019
  - Warpで出力させたものをeditbinで編集するため `C++ によるデスクトップ開発` が必要です。

### お手軽ビルド

1. `publish.bat` を実行します。
2. なんやかんやあって `tmp/KyoshinEewViewer.exe` が生成されます。

### 注意点

[Warp](https://github.com/dgiagio/warp) を使用しています。  
これはファイルの更新日時を見て一時ファイルを更新しているため、状況によっては最新バージョンが適用されない可能性があります。

## Toolsについて

### TopoJsonConverter

TopoJsonを簡略化するものです。  
単一ポリゴンのものしか対応していません。

### TrTimeTableConverter

走時表を変換するものです。

### ObservationPointEditor

観測点を編集するやつです。  
未完成ですが、利用は可能です。
