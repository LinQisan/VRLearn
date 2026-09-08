# VRLearn

Meta Quest 2 向けの交通安全体験を作る Unity プロジェクトです。Unity Editor では、ヘッドセットなしでキーボードとマウスを使って動かせます。

## フォルダ構成

- `Assets/`：シーン、スクリプト、モデルなどの素材。
- `Packages/`：使用する Unity パッケージとバージョンの情報。
- `ProjectSettings/`：描画、入力、ビルドなどのプロジェクト設定。
- `Docs/`：開発メモと検証記録。

`Assets` 内は次のように分かれています。

| フォルダ | 内容 |
| --- | --- |
| `_Project/` | このプロジェクト用のコードと素材 |
| `ThirdParty/` | 外部から導入したモデルやプラグイン |
| `Resources/` | 実行時に読み込む素材と SDK 設定 |
| `Plugins/` | Android 用の設定ファイル |
| `TextMesh Pro/` | テキスト表示用の共通リソース |
| `Oculus/`、`XR/` | VR 関連の設定 |
| `StreamingAssets/` | そのままビルドに含めるファイルの置き場（現在は空） |

`_Project` の主なフォルダ：

- `Scripts/`：動作を制御する C# スクリプト。
- `Scenes/`：タイトルの `TraficAcidentTitle_Meta.unity` と、体験用の `TraficAcident_Meta.unity`。
- `Prefabs/`：車両や雨の Prefab。
- `ScenarioDefinitions/`：`Scenario_00.asset` ～ `Scenario_09.asset` のシナリオ設定。
- `Models/`、`Animations/`：モデル、生成メッシュ、アニメーション。
- `Materials/`、`Textures/`、`Shaders/`：見た目に使う素材。
- `Audio/`、`UI/`：音声、フォント、UI 画像。
- `World/`：地形データ。
- `Input/`、`Settings/`、`XR/`：入力、描画、VR の設定。
- `Editor/`、`Tests/`：開発用ツールとテスト。

## 開き方・動かし方

1. Unity Hub でこのフォルダを追加し、**Unity 6000.3.19f1** で開きます。
2. パッケージの読み込みと素材のインポートが終わるまで待ちます。
3. `Assets/_Project/Scenes/TraficAcidentTitle_Meta.unity` を開き、Play を押します。
4. マウスでメニューを選択します。体験中は W/A/S/D で移動し、マウスの右ボタンを押しながら視点を動かせます。

素材を移動するときは、参照を保つために `.meta` ファイルも一緒に移動してください。
