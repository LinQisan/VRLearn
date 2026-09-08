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

    //イベントナンバー2用の遅延車の順番
    int AcidentCarNumber;

    //waypointを格納する変数
    Transform pointsParent;

    //事故車生産フラグをオン
    public void AcidentCarChange()
    {
        //Debug.Log("生産");

        //車の生産を中止
        AccidentCarSpawn = 1;
    }
    //コルーチンを実行するメソッド(AccidentCarFactoryから受け取る)
    public void CoroutineStart()
    {
        if (TrafficAccidentState.IsFrozen)
            return;
        StartCoroutine("AcidentCar");
    }
    //事故車生産(上のメソッドで起動)
    public IEnumerator AcidentCar()
    {
        if (TrafficAccidentState.IsFrozen)
            yield break;
        //Debug.Log("生産");
        //イベントナンバー0
        if (EventNumber == 0)
        {
            //少し遅れて事故車を出す
            yield return new WaitForSeconds(6f);
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー1
        if (EventNumber == 1)
        {
            yield return new WaitForSeconds(0.5f);
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);

            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー2
        if (EventNumber == 2)
        {
            yield return new WaitForSeconds(0.5f);
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
            //向き調整
            Car.transform.eulerAngles = new Vector3(0, 90, 0);

            //2台目以降なら位置調整
            if(AcidentCarNumber >= 1)
            {
                Car.transform.position += new Vector3(-30f, 0, 0);
            }

            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;

            //2台目以降との区別をつける
            AcidentCarNumber += 1;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー3と5
        if (EventNumber == 3 || EventNumber == 5)
        {
            yield return new WaitForSeconds(0.5f);
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
            //向き調整
            Car.transform.eulerAngles = new Vector3(0, -90, 0);

            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;

            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー4
        if (EventNumber == 4)
        {
            yield return null;
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
            //向き調整
            Car.transform.eulerAngles = new Vector3(0, 180, 0);
            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー6と7
        if (EventNumber == 6 || EventNumber == 7)
        {
            yield return new WaitForSeconds(0.5f);
            Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);

            //車にゲームディレクターから受け取ったIDを登録
            Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
            //ゲームディレクターの車IDを更新
            gamedirector.GetComponent<GameDirector>().CarID += 1;
            //waypoint設定
            Car.GetComponent<CarController>().pointsParent = pointsParent;
            //事故車フラグをオン
            Car.GetComponent<CarController>().AcidentCar = true;
            Car.GetComponent<CarController>().InitializeForSpawn();
        }
        //イベントナンバー8
        if(EventNumber == 8)
        {
            if(this.gameObject.name == "CarFactory_Acident_Right")
            {
                yield return new WaitForSeconds(0.5f);
                Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
                //向き調整
                Car.transform.eulerAngles = new Vector3(0, -90, 0);

                //車にゲームディレクターから受け取ったIDを登録
                Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;

                //ゲームディレクターの車IDを更新
                gamedirector.GetComponent<GameDirector>().CarID += 1;
                //waypoint設定
                Car.GetComponent<CarController>().pointsParent = pointsParent;
                //事故車フラグをオン
                Car.GetComponent<CarController>().AcidentCar = true;

                //2台目以降との区別をつける
                AcidentCarNumber += 1;
                Car.GetComponent<CarController>().InitializeForSpawn();
            }
            if (this.gameObject.name == "CarFactory_Acident_Left")
            {
                //左から来る車はかなり遅らせる
                yield return new WaitForSeconds(6.5f);
                Car = VehiclePool.Instance.Get(CarPrefab, transform.position, Quaternion.identity);
                //向き調整
                Car.transform.eulerAngles = new Vector3(0, 90, 0);

                /*//2台目以降なら位置調整
                if (AcidentCarNumber >= 1)
                {
                    Car.transform.position += new Vector3(-30f, 0, 0);
                }*/

                //車にゲームディレクターから受け取ったIDを登録
                Car.GetComponent<CarController>().CarID = gamedirector.GetComponent<GameDirector>().CarID;
                //ゲームディレクターの車IDを更新
                gamedirector.GetComponent<GameDirector>().CarID += 1;
                //waypoint設定
                Car.GetComponent<CarController>().pointsParent = pointsParent;
                //事故車フラグをオン
                Car.GetComponent<CarController>().AcidentCar = true;

                //2台目以降との区別をつける
                AcidentCarNumber += 1;
                Car.GetComponent<CarController>().InitializeForSpawn();
            }
        }
        Debug.Log("生産");
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

        //横方向に車生産
        if (EventNumber == 0)
        {
            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Right);
            }
        }
        //縦方向に車生産
        if (EventNumber == 1)
        {
            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left2);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Right2);
            }
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident1);
            }
        }
        if (EventNumber == 4)
        {
            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left2);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Right2);
            }
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident4);
            }
        }
        //通常車生産なし
        if (EventNumber == 2)
        {
            /*//waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left);
            }*/
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident2);
            }
        }
        //通常車生産なし
        if (EventNumber == 3)
        {
            /*//waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left);
            }*/
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident3);
            }
        }
        if (EventNumber == 5)
        {
            /*//waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Left);
            }*/
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident5);
            }
        }
        if (EventNumber == 6)
        {
            //即車生産
            TimeProgress = 4f;

            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentLeft);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentRight);
            }
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident6);
            }
        }
        if (EventNumber == 7)
        {
            //即車生産
            TimeProgress = 4f;

            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentLeft);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentRight);
            }
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident7);
            }
        }
        if (EventNumber == 8)
        {
            if (this.gameObject.tag == "AcidentFactory")
            {
                if(this.gameObject.name == "CarFactory_Acident_Left")
                {
                    //左から来る車用
                    pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident8Left);
                }
                else if (this.gameObject.name == "CarFactory_Acident_Right")
                {
                    //右から来る車用
                    pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident8Right);
                }
            }
        }
        if (EventNumber == 9)
        {
            //即車生産
            TimeProgress = 4f;

            //waypointの親オブジェクトを格納
            if (this.gameObject.tag == "LeftFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentLeft);
            }
            if (this.gameObject.tag == "RightFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.AccidentRight);
            }
            if (this.gameObject.tag == "AcidentFactory")
            {
                pointsParent = WaypointRouteRegistry.Resolve(WaypointRouteId.Accident6);
            }
        }

        // Prefer a route authored under the active scenario. The shared registry
        // historically pointed scenarios 6, 7 and 9 at each other's inactive roots.
        var localScenarioPath = ResolveLocalScenarioPath();
        if (localScenarioPath != null)
            pointsParent = localScenarioPath;
        if (pointsParent == null)
        {
            Debug.LogError($"{name} has no waypoint route for scenario {EventNumber}.", this);
            enabled = false;
            return;
        }

        AccidentCarSpawn = 0;
    }

    Transform ResolveLocalScenarioPath()
    {
        Transform scenarioArea = transform.parent;
        if (scenarioArea == null || !scenarioArea.name.StartsWith("AcidentAreas"))
            return null;

        if (CompareTag("AcidentFactory"))
        {
            if (name.EndsWith("_Left"))
                return scenarioArea.Find("WayPointContainer_Acident_Left");
            if (name.EndsWith("_Right"))
                return scenarioArea.Find("WayPointContainer_Acident_Right");
            return scenarioArea.Find("WayPointContainer_Acident");
        }
        if (CompareTag("LeftFactory"))
            return scenarioArea.Find("WayPointContainer_Acident_Left/Left");
        if (CompareTag("RightFactory"))
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
                    if (((EventNumber == 1 || EventNumber == 6 || EventNumber == 7 || EventNumber == 9) && this.gameObject.tag == "LeftFactory") || (EventNumber == 4 && this.gameObject.tag == "LeftFactory"))
                    {
                        Car.transform.eulerAngles = new Vector3(0, 90, 0);
                    }
                    if (((EventNumber == 1 || EventNumber == 6 || EventNumber == 7 || EventNumber == 9) && this.gameObject.tag == "RightFactory")|| (EventNumber == 4 && this.gameObject.tag == "RightFactory"))
                    {
                        Car.transform.eulerAngles = new Vector3(0, -90, 0);
                    }

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
