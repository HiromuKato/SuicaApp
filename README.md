# SuicaApp
[PaSoRi](https://www.sony.co.jp/Products/felica/consumer/index.html)でSuicaの情報を読み取り、csvに出力するWPFアプリ（交通費計算用）

ビルドして出来上がった実行ファイルと同じ階層に以下ファイルを格納する必要があります。
- OK.wav (Suicaの読み取りが成功したときに鳴る音)
- NG.wav (Suicaの読み取りが失敗したときに鳴る音)
- [felicalib.dll](https://github.com/tmurakam/felicalib/releases) (本プロジェクト内にも格納しています)
- StationCode.csv ([参考サイト](https://github.com/MasanoriYONO/StationCode))

PaSoRiにSuicaを置き「Suica読み取り」ボタンを押すと実行ファイルと同じ階層のoutputディレクトリ配下にcsvファイルを生成します。

本アプリによる出力データは正確性を保証するものではありません。