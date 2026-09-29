using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarFactory : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    GameObject ovrcamera;
    GameObject gamedirector;

    //事故を起こす車が生産された
    public int AccidentCarSpawn;

    //経過時間を管理する変数
    float TimeSpan;
    float TimeProgress;

    //プレハブを格納する変数
    public GameObject CarPrefab;
    GameObject Car;

    //イベントナンバーを管理する変数
    int EventNumber;

    //waypointを格納する変数
    Transform pointsParent;

    //事故車生産フラグをオン
    public void AcidentCarChange()
    {
        //Debug.Log("生産");

        //車の生産を中止
        AccidentCarSpawn = 1;
    }
    /// <summary>
    /// Spawns one accident car from this factory on its route. Called by
    /// AccidentCarFactory following the scenario's accident schedule.
    /// </summary>
    public void SpawnAccidentCar(AccidentLaunch launch)
    {
        if (TrafficAccidentState.IsFrozen || !enabled || pointsParent == null)
            return;
        Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
        if (launch.overrideYaw)
            Car.transform.eulerAngles = new Vector3(0f, launch.yaw, 0f);
        Car.transform.position += launch.offset;

        var controller = Car.GetComponent<CarController>();
        //車にゲームディレクターから受け取ったIDを登録し、IDを更新
        controller.CarID = gamedirector.GetComponent<GameDirector>().CarID;
        gamedirector.GetComponent<GameDirector>().CarID += 1;
        controller.pointsParent = pointsParent;
        //事故車フラグをオン
        controller.AcidentCar = true;
        controller.InitializeForSpawn();
    }

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        this.ovrcamera = OpenXRScene.CameraControllerObject;
        var context = GameplaySceneContext.Instance;
        this.gamedirector = context != null && context.Director != null
            ? context.Director.gameObject
            : FindFirstObjectByType<GameDirector>()?.gameObject;
        if (gamedirector == null)
        {
            Debug.LogError("CarFactory requires a GameDirector.", this);
            enabled = false;
            return;
        }

        //イベントナンバーを格納
        EventNumber = gamedirector.GetComponent<GameDirector>().EventNumber;

        //時間を格納
        TimeProgress = 0;
        TimeSpan = Random.Range(4.0f, 6.0f);

        // cycling scenarios start with a car almost due
        if (EventNumber == 6 || EventNumber == 7 || EventNumber == 9)
            TimeProgress = 4f;
        pointsParent = ResolveRoute(transform, EventNumber);
        if (pointsParent == null)
        {
            Debug.LogError($"{name} has no waypoint route for scenario {EventNumber}.", this);
            enabled = false;
            return;
        }

        AccidentCarSpawn = 0;
    }

    /// <summary>
    /// The route a factory drives for a built-in scenario: chosen by the factory's tag, name and the
    /// scenario id; a route authored under the active scenario's accident area wins.
    /// </summary>
    public static Transform ResolveRoute(Transform factory, int eventNumber)
    {
        var local = ResolveLocalScenarioPath(factory);
        if (local != null)
            return local;
        var left = factory.CompareTag("LeftFactory");
        var right = factory.CompareTag("RightFactory");
        var accident = factory.CompareTag("AcidentFactory");
        WaypointRouteId? id = eventNumber switch
        {
            0 when left => WaypointRouteId.Left,
            0 when right => WaypointRouteId.Right,
            1 when left => WaypointRouteId.Left2,
            1 when right => WaypointRouteId.Right2,
            1 when accident => WaypointRouteId.Accident1,
            4 when left => WaypointRouteId.Left2,
            4 when right => WaypointRouteId.Right2,
            4 when accident => WaypointRouteId.Accident4,
            2 when accident => WaypointRouteId.Accident2,
            3 when accident => WaypointRouteId.Accident3,
            5 when accident => WaypointRouteId.Accident5,
            6 when left => WaypointRouteId.AccidentLeft,
            6 when right => WaypointRouteId.AccidentRight,
            6 when accident => WaypointRouteId.Accident6,
            7 when left => WaypointRouteId.AccidentLeft,
            7 when right => WaypointRouteId.AccidentRight,
            7 when accident => WaypointRouteId.Accident7,
            8 when accident && factory.name == "CarFactory_Acident_Left" => WaypointRouteId.Accident8Left,
            8 when accident && factory.name == "CarFactory_Acident_Right" => WaypointRouteId.Accident8Right,
            9 when left => WaypointRouteId.AccidentLeft,
            9 when right => WaypointRouteId.AccidentRight,
            9 when accident => WaypointRouteId.Accident6,
            _ => null
        };
        return id.HasValue ? WaypointRouteRegistry.Resolve(id.Value) : null;
    }

    /// <summary>Heading of regular traffic from a side factory (vertical and cycling scenarios).</summary>
    public static float? RegularTrafficYaw(Transform factory, int eventNumber)
    {
        if (eventNumber != 1 && eventNumber != 4 && eventNumber != 6 && eventNumber != 7 && eventNumber != 9)
            return null;
        if (factory.CompareTag("LeftFactory")) return 90f;
        if (factory.CompareTag("RightFactory")) return -90f;
        return null;
    }

    static Transform ResolveLocalScenarioPath(Transform factory)
    {
        Transform scenarioArea = factory.parent;
        if (scenarioArea == null || !scenarioArea.name.StartsWith("AcidentAreas"))
            return null;

        if (factory.CompareTag("AcidentFactory"))
        {
            if (factory.name.EndsWith("_Left"))
                return scenarioArea.Find("WayPointContainer_Acident_Left");
            if (factory.name.EndsWith("_Right"))
                return scenarioArea.Find("WayPointContainer_Acident_Right");
            return scenarioArea.Find("WayPointContainer_Acident");
        }
        if (factory.CompareTag("LeftFactory"))
            return scenarioArea.Find("WayPointContainer_Acident_Left/Left");
        if (factory.CompareTag("RightFactory"))
            return scenarioArea.Find("WayPointContainer_Acident_Right/Right");
        return null;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (TrafficAccidentState.IsFrozen)
            return;

        //事故工場では行わない
        if (this.gameObject.tag != "AcidentFactory") 
        {
            //事故エリアに入ったら、生産を中止
            if (AccidentCarSpawn == 0)
            {
                TimeProgress += Time.deltaTime;     //時間をカウントする

                //経過時間が繰り返す間隔を経過したら
                if (TimeProgress >= TimeSpan)
                {
                    //ここで処理を実行
                    //車生産
                    Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);

                    //車にゲームディレクターから受け取ったIDを登録
                    Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;

                    //ゲームディレクターの車IDを更新
                    gamedirector.GetComponent<GameDirector>().CarID += 1;

                    //向きを調整
                    var yaw = RegularTrafficYaw(transform, EventNumber);
                    if (yaw.HasValue)
                        Car.transform.eulerAngles = new Vector3(0, yaw.Value, 0);

                    //waypoint設定
                    Car.GetComponent<CarController>().pointsParent = pointsParent;

                    // Pooled instances may carry AcidentCar=true from an
                    // accident life; ordinary traffic must reset it every spawn.
                    Car.GetComponent<CarController>().AcidentCar = false;
                    Car.GetComponent<CarController>().InitializeForSpawn();

                    TimeProgress = 0;   //経過時間をリセットする
                    TimeSpan = Random.Range(4.0f, 6.0f);//スパン変更
                }
            }
        }
    }
}
