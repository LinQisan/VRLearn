# カスタム場面（JSON）

このフォルダの `*.json` が、Unity の場面を編集せずに追加できる「カスタム場面」です。

- 形式: [`scenario.schema.json`](scenario.schema.json)（座標は Unity のワールド座標、メートル。x と z だけを書きます）
- ひな形: `templates/builtin-01.json` 〜 `builtin-10.json`（組み込みの 10 場面を書き出したもの）
- Unity で試す: `Tools/VRLearn/Custom Scenarios/Play Scenario File…`
- まとめて確認: `Tools/VRLearn/Custom Scenarios/Validate Scenario Files`
- ひな形の更新: `Tools/VRLearn/Custom Scenarios/Export Built-in Scenarios As Templates`

場面はシナリオエディタ（[Hikone Traffic Simulator](https://github.com/LinQisan/hikone-traffic-sim) の `editor/`。隣の `../hikone-traffic-sim` で `node serve.mjs` → http://localhost:8770/editor/ を開くと、このフォルダに保存できます）で作れます。Quest へは、エディタの「⇪ Questに送る」でコピーします（`/sdcard/Android/data/<package>/files/Scenarios`）。Quest ではタイトル画面の「カスタム」タブに出ます。

地図は彦根・京橋のみ対応しています。CSV では `EventNumber` が 100、最後の列 `CustomScenario` にファイルの `id` が入ります。

## 地図（シナリオエディタ用）

`maps/hikone-kyobashi/` は Unity から書き出した地図です（`Tools/VRLearn/Custom Scenarios/Export Map For Scenario Editor`。手で編集しないでください）。

- `map.png`: 真上からの画像（10 px/m、上が +z、右が +x）
- `map.json`: 座標変換（`px = (x - xMin) * pixelsPerMeter`、上からの `py = (zMax - z) * pixelsPerMeter`）、路面（車道・歩道・スロープ・地面の三角形）、見えない壁、組み込み場面の車線、信号の位置、使える車種
