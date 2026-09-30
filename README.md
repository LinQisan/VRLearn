# VRLearn

Meta Quest 2 向けの、歩行者向け交通安全 VR 体験・実験用 Unity プロジェクトです（一部の場面は自転車で体験します）。体験の舞台は彦根・京橋口の交差点（夢京橋キャッスルロードと中堀通り）です。Unity Editor では、ヘッドセットなしでもキーボードとマウスで動かせます。

- 開発ルールと作業手順 → [AGENTS.md](AGENTS.md)（AI エージェント・開発者向け）
- 設計の考え方と決定事項 → [DESIGN.md](DESIGN.md)
- 彦根シーンの詳細 → [Docs/Hikone/README.md](Docs/Hikone/README.md)

## 動かし方

1. Unity Hub でこのフォルダを追加し、**Unity 6000.3.19f1** で開きます。
2. パッケージの読み込みと素材のインポートが終わるまで待ちます。
3. `Assets/_Project/Scenes/Title.unity` を開き、Play を押します。
4. タイトル画面で条件を選び、「開始 / Start」を押すと彦根シーンが始まります。

| 操作 | Editor | Quest |
| --- | --- | --- |
| メニュー選択 | マウスクリック | コントローラーで指してトリガー |
| 移動 | W/A/S/D | 左スティック |
| 視点 | 右クリックしながらマウス | 頭の向き |
| 結果画面 | Enter/Space でタイトルへ、R で同じ条件の再体験 | A/X でタイトルへ、画面の「同じ条件で再体験」ボタン |

## シーン

| シーン | 役割 |
| --- | --- |
| `TraficAcidentTitle_Meta` | タイトル。すべての選択肢をタイル状に並べ、トリガー 1 回で選べる |
| `TraficAcident_Hikone_Meta` | 体験シーン（彦根・京橋）。現在はこのシーンだけに入れる |
| `TraficAcident_Meta` | 旧・標準シーン。回帰テストの基準として残しているが、タイトルからは入れない |

場面は 0–9 の 10 種類（6・7・9 は自転車）で、交通・事故のロジックはどちらのシーンでも共通です。

## フォルダ構成

| フォルダ | 内容 |
| --- | --- |
| `Assets/_Project/` | このプロジェクトのコードと素材（`Scripts/`、`Scenes/`、`ScenarioDefinitions/`、`Prefabs/`、`Editor/`、`Tests/` など） |
| `Assets/_Project/Scripts/` | 機能別：`Core/`（全体の流れ・CSV）、`Scenario/`（場面）、`Traffic/`（車・信号）、`Player/`（体験者）、`XR/`（VR 入力）、`Accident/`（事故・リプレイ・振り返り）、`Title/`（タイトル）、`UI/`、`Recording/` |
| `ScenarioEditor/` | カスタム場面を作るブラウザ用エディタ |
| `Scenarios/` | カスタム場面（JSON）、組み込み場面のひな形、エディタ用の地図 |
| `Assets/_Project/Hikone/` | 彦根シーンの生成済みモデル・マテリアル・プレハブ・配置データ |
| `Art/Hikone/` | 彦根アセットの生成元（Blender 用 Python スクリプト） |
| `Assets/ThirdParty/` | 外部から導入したモデルやプラグイン |
| `Docs/` | `Hikone/`（彦根シーンの説明）、`History/`（過去の作業記録） |

素材を移動するときは、参照を保つために `.meta` ファイルも一緒に移動してください。
