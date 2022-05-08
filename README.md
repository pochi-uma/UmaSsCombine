# UmaSsCombine
## このツールは？
「ウマ娘詳細」画面の「継承タブ」の複数枚画像を1枚に結合するツールです

## 動作環境
Windows10 64bit  
(Windows11でも多分動きます)  
※64bit版でしか動作しません  
※ウマ娘が64bit版でしか動作しないのでウマ娘が動く環境なら問題ありません

## インストール方法
zipファイルを適当なフォルダに解凍して下さい  
追加で、以下のインストールが必要です  

[.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet/3.1/runtime/)    
Run desktop appsのDownload x64  

## アンインストール方法
レジストリ等使用していませんのでフォルダの削除だけです

## 使い方
![how2](readme_images/UmaSsCombine.gif)  
- 結合したい複数の「継承タブ」画面のスクリーンショットをUmaSsCombine.exeにドラッグ&ドロップして下さい  
結合元の画像を同じフォルダに結合した画像を出力します(ファイル名は「年月月日時分秒ミリ秒.png」)

- 結合元画像の処理順  
デフォルトはファイル名の昇順に処理を実行していきます  
処理順を変更したい場合は、以下を参考にしてください 
    - 降順に変更したい  
config.jsonの「sortOrder」の"ascending"を"descending"に変更してください  
    - ファイルの作成日時順に処理をさせたい  
config.jsonの「sortTarget」を"timeStamp"に変更してください  
(昇順・降順は「sortOrder」で指定してください)  
    - ドラッグ&ドロップした順に処理させたい  
config.jsonの「sortTarget」を"none"に変更してください  
(昇順・降順は指定できません)

## オプション機能
 - デフォルト結合画面
 ![normal](readme_images/normal.png)
 - 左右を切り落とし機能  
 config.jsonの「deleteSideMargin」をtrueに変更してください
 ![deleteSideMargin](readme_images/deleteSideMargin.png)
 - スクロールバー消去機能  
 config.jsonの「deleteScrollBar」をtrueに変更してください
 ![deleteScrollBar](readme_images/deleteScrollBar.png) 

## 注意点
- 結合元の画像はすべて同じ解像度にして下さい  
1枚でも解像度が違うとエラーと判定します
- 結合処理において、最下段のスキルを元に次画像結合位置を決めています  
最低1行分はスキルを被せて撮影すると結合処理が上手くいきます(下の赤枠参照)  
![exsample](readme_images/exsample1.png)
- 結合に失敗した場合は、結合元の画像を同じフォルダに「年月月日時分秒ミリ秒._error.txt」が生成されます  
エラー内容とスクリーンショットを頂ければ、対応します

## エラー内容について
- 2つ以上の画像ファイルを指定してください  
-> そのままの意味です
- 異なる解像度の画像が入力されています  
-> 結合対象の画像はすべて同じ解像度にしてください
- n番目の画像のテンプレートマッチに失敗しました  
- n番目とn+1番目の画像の一致箇所が見つかりませんでした  
-> 画像間で最低1行分はスキルを被せて撮影してくだい
- 境界の取得に失敗しました(幅:XX 高さ:XX 左:XX 右:XX)  
- n番目の画像の境界(下)の取得に失敗しました  
-> 結合に失敗したスクショとエラー内容を連絡ください

## エラー報告等連絡先
[ポチTwitter](https://twitter.com/aoneko_uma)

## Special Thanks
アプリケーションのアイコンは「[なお](https://twitter.com/Bcat151)」さんに頂きました  
