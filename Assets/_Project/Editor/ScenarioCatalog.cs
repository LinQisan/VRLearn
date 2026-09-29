using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Canonical content of the ten scenarios: names, learning goals, explanations and the accident
/// schedule. The schedules reproduce the timings that used to be hard-coded in
/// AccidentCarFactory/CarFactory (one-frame waits became 0.02 s). The ScenarioDefinitionAsset
/// files are what the game reads; this menu rewrites them from the table below.
/// </summary>
public static class ScenarioCatalog
{
    const string Folder = "Assets/_Project/ScenarioDefinitions";

    struct Entry
    {
        public string display, shortTitle, shortEn, goal, situation, point;
        public ScenarioSetting setting;
        public ScenarioFactory[] stop;
        public AccidentLaunch[] launches;
    }

    static AccidentLaunch L(ScenarioFactory f, float delay, float? yaw = null, float offsetX = 0f) => new AccidentLaunch
    {
        factory = f,
        delaySeconds = delay,
        overrideYaw = yaw.HasValue,
        yaw = yaw ?? 0f,
        offset = new Vector3(offsetX, 0f, 0f)
    };

    static readonly ScenarioFactory[] None = new ScenarioFactory[0];
    const ScenarioFactory Area = ScenarioFactory.AreaAccident;

    static Dictionary<int, Entry> Table() => new Dictionary<int, Entry>
    {
        [0] = new Entry
        {
            display = "横断歩道：手前の車線を右から来る車",
            shortTitle = "右から来る車", shortEn = "Car from the right", setting = ScenarioSetting.Crossing,
            goal = "車の流れが切れても、手前の車線を右から来る車を確かめてから渡る。",
            situation = "横断歩道を渡り始めたところで、右から来た車とぶつかりました。",
            point = "渡る前に右・左・右を見て、車が来ないことを確かめてから渡りましょう。",
            stop = new[] { ScenarioFactory.Right },
            launches = new[] { L(ScenarioFactory.Right, 6f) }
        },
        [1] = new Entry
        {
            display = "交差点：横から曲がってくる車",
            shortTitle = "曲がってくる車", shortEn = "Turning car", setting = ScenarioSetting.Crossing,
            goal = "交差点では、横やうしろから曲がってくる車にも気をつける。",
            situation = "交差点の横断歩道を渡っているとき、左うしろ（キャッスルロード側）から右に曲がってきた車とぶつかりました。",
            point = "前だけでなく、曲がってくる車が来ないか、横やうしろも確かめましょう。",
            stop = new[] { ScenarioFactory.Left2 },
            launches = new[] { L(Area, 0.5f), L(Area, 4f) }
        },
        [2] = new Entry
        {
            display = "道路の途中：左から続けて来る車",
            shortTitle = "左から続けて来る車", shortEn = "Another car from the left", setting = ScenarioSetting.MidBlock,
            goal = "1台通りすぎても、すぐ次の車が来ないか確かめる。",
            situation = "止まっているトラックの横から渡ろうとしたとき、左から続けて来た車とぶつかりました。",
            point = "1台通ったらもう一度左右を見て、次の車が来ないか確かめましょう。",
            stop = None,
            launches = new[] { L(Area, 0.5f, 90f), L(Area, 0.52f, 90f, -30f), L(Area, 4.02f, 90f, -30f) }
        },
        [3] = new Entry
        {
            display = "道路の途中：止まっている車の陰から来る車",
            shortTitle = "トラックの陰から来る車", shortEn = "Car hidden by a truck", setting = ScenarioSetting.MidBlock,
            goal = "止まっている車の陰は見えない。陰から出る前に止まって確かめる。",
            situation = "止まっているトラックの陰から出たところで、右から来た車とぶつかりました。",
            point = "車の陰から出るときは、いったん止まって顔を出し、右と左を確かめましょう。",
            stop = None,
            launches = new[] { L(Area, 0.5f, -90f), L(Area, 4f, -90f), L(Area, 11f, -90f) }
        },
        [4] = new Entry
        {
            display = "交差点：うしろから曲がってくる車",
            shortTitle = "うしろから曲がる車", shortEn = "Car turning from behind", setting = ScenarioSetting.Crossing,
            goal = "曲がってくる車はうしろから来る。渡る前にうしろも確かめる。",
            situation = "橋の側から交差点を渡っているとき、うしろ（橋の方）から左に曲がってきた車とぶつかりました。",
            point = "渡る前に振り返って、うしろから曲がってくる車がいないか確かめましょう。",
            stop = new[] { ScenarioFactory.Right2 },
            launches = new[] { L(Area, 0.02f, 180f), L(Area, 3.52f, 180f) }
        },
        [5] = new Entry
        {
            display = "道路の途中：スピードを出して止まらない車",
            shortTitle = "スピードを出す車", shortEn = "Speeding car", setting = ScenarioSetting.MidBlock,
            goal = "遠くに見えても、速い車はすぐに来る。十分に離れているときだけ渡る。",
            situation = "車はまだ遠いと思って渡り始めたところで、スピードを出した車にぶつかられました。車は止まらずに走り去りました。",
            point = "速い車はあっという間に近くまで来ます。車が遠くにいて、ゆっくりのときだけ渡りましょう。",
            stop = None,
            launches = new[] { L(Area, 0.5f, -90f), L(Area, 4f, -90f), L(Area, 11f, -90f) }
        },
        [6] = new Entry
        {
            display = "自転車：駐車場から急に出てくる車",
            shortTitle = "出入口から出る車", shortEn = "Car pulling out", setting = ScenarioSetting.Bicycle,
            goal = "出入口の前ではスピードを落とし、車が出てこないか見る。",
            situation = "自転車で走っているとき、駐車場の出口から急に出てきた車とぶつかりました。",
            point = "出入口の前ではスピードを落とし、出てくる車がいないか確かめましょう。",
            stop = new[] { ScenarioFactory.AreaAccidentRight },
            launches = new[] { L(Area, 0.5f) }
        },
        [7] = new Entry
        {
            display = "自転車：出入口の前を通るときの車",
            shortTitle = "出入口の前を通る", shortEn = "Passing a driveway", setting = ScenarioSetting.Bicycle,
            goal = "出入口の前を通りすぎるまで、横から出てくる車に気をつける。",
            situation = "自転車で駐車場の出入口の前を通っているとき、出てきた車に横からぶつかられました。",
            point = "出入口の前は止まれる速さで通り、車が見えたらすぐ止まりましょう。",
            stop = new[] { ScenarioFactory.AreaAccidentRight },
            launches = new[] { L(Area, 0.5f) }
        },
        [8] = new Entry
        {
            display = "道路の途中：左右から時間差で来る車",
            shortTitle = "左右から時間差で来る車", shortEn = "Cars from both sides", setting = ScenarioSetting.MidBlock,
            goal = "片方を見たら反対側も。渡っている間も左右を見続ける。",
            situation = "右から来た車が通りすぎたあと渡り始め、左から来た車とぶつかりました。",
            point = "片方の車が通りすぎても、反対側を確かめてから渡り、渡っている間も左右を見ましょう。",
            stop = None,
            launches = new[]
            {
                L(ScenarioFactory.AreaAccidentRight, 0.5f, -90f),
                L(ScenarioFactory.AreaAccidentLeft, 6.5f, 90f),
                L(ScenarioFactory.AreaAccidentRight, 12.5f, -90f)
            }
        },
        [9] = new Entry
        {
            display = "自転車：道路の右側を走る（逆走）",
            shortTitle = "右側を走る自転車", shortEn = "Riding on the wrong side", setting = ScenarioSetting.Bicycle,
            goal = "自転車は左側通行。右側を走ると前から来る車とぶつかりやすい。",
            situation = "自転車で道路の右側を走っていて、前から来た車とぶつかりました。",
            point = "自転車は車道の左側を走りましょう。前から来る車とすれ違うのはとても危険です。",
            stop = None,
            launches = new AccidentLaunch[0]      // regular traffic in the oncoming lane is the hazard
        }
    };

    [MenuItem("Tools/VRLearn/Scenarios/Write Scenario Catalog To Assets")]
    public static void Apply()
    {
        var table = Table();
        foreach (var guid in AssetDatabase.FindAssets("t:ScenarioDefinitionAsset", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDefinitionAsset>(path);
            if (asset == null || !table.TryGetValue(asset.id, out var e))
                continue;
            asset.displayName = e.display;
            asset.shortTitle = e.shortTitle;
            asset.shortTitleEn = e.shortEn;
            asset.setting = e.setting;
            asset.learningGoal = e.goal;
            asset.eventSummary = $"事故の状況：{e.situation}\n安全確認のポイント：{e.point}";
            asset.stopTraffic = e.stop;
            asset.accidentLaunches = e.launches;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Scenarios] catalog written to " + table.Count + " scenario assets");
    }
}
