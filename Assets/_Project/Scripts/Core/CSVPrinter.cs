using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.IO;
using System.Globalization;

public class CSVPrinter : MonoBehaviour
{
    //スクリプトを格納する変数
    [SerializeField]GameDirector gamedirector;

    //ゲームオブジェクトを格納する変数
    GameObject ovrcamera;

    //CSVに出力する変数
    public int EventNumber;
    public int Height;
    public int Weight;
    public int DieFlash;
    public int Gender;
    public int Age;
    public int License;
    [FormerlySerializedAs("OnAcident")] public int OnAccident;
    public int OnGoal;
    int AfterAccident;
    public float Hz;
    public int SmartPhone;
    public int Incident;
    public int Weather;
    public int SkyTime;

    //車と人のクラス
    CarData cardata;
    HumanData humandata;

    //車と人から受け取った変数を格納するリスト
    [SerializeField] List<CarData> CarDataList;
    [SerializeField] List<HumanData> HumanDataList;


    //ゲーム開始からの経過時間
    public float NowTime;

    bool saved;

    /// <summary>Folder that receives the CSV files (Application.persistentDataPath).</summary>
    public static string OutputFolder => Application.persistentDataPath;

    /// <summary>Paths written by the last save (for tests and tooling).</summary>
    public string[] LastWrittenFiles { get; private set; } = new string[0];

    // Numbers are written culture-invariant ("1.5", never "1,5") so the CSV parses the same everywhere.
    static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

    //テスト用変数
    public int[] logData; // Logデータの宣言

    //車から受け取る変数を格納するクラス
    [System.Serializable]
    public class CarData
    {
        //車のポジション
        public Vector3 CarPosition = new Vector3 (0, 0, 0);
        //車のID
        public int CarID = 0;
        //車が事故車かどうか
        [FormerlySerializedAs("AcidentCar")] public int AccidentCar = 0;
        //車の時間
        public float CarTime = 0;

        //コンストラクタ
        public CarData(Vector3 position, int carid , int accidentcar , float cartime)
        {
            CarPosition = position;
            CarID = carid;
            AccidentCar = accidentcar;
            CarTime = cartime;
        }
    }

    //人(カメラ)から受け取る変数を格納するクラス
    [System.Serializable]
    public class HumanData
    {
        //人(カメラ)のポジション
        public Vector3 HumanPosition = new Vector3(0, 0, 0);
        //人のローテーション
        public Vector3 HumanRotation = new Vector3 (0, 0, 0);
        //人の時間
        public float HumanTime = 0;

        //事故を起こしたかどうか
        [FormerlySerializedAs("AfterAcident")] public int AfterAccident = 0;

        //事故過程
        [FormerlySerializedAs("AcidentProgress")] public int AccidentProgress = 0;

        //コンストラクタ
        public HumanData(Vector3 position , Vector3 rotation , float humantime , int afteraccident , int accidentprogress)
        {
            HumanPosition =position;
            HumanRotation = rotation;
            HumanTime = humantime;
            AfterAccident = afteraccident;
            AccidentProgress = accidentprogress;
        }
        
    }

    //車からデータを受け取り、クラスに保存するメソッド
    public void CarDataReceiver(Vector3 position , int carid , int accidentcar)
    {
        cardata = new CarData(position,carid, accidentcar, this.NowTime);
        CarDataList.Add(cardata);
    }

    //人からデータを受け取り、クラスに保存するメソッド
    public void HumanDataReceiver(Vector3 position , Vector3 rotation)
    {

        if(ovrcamera.GetComponent<LegacyAccidentPresentation>().Accident == 1)
        {
            AfterAccident = 1;
        }
        if(ovrcamera.GetComponent<LegacyAccidentPresentation>().AccidentProgress == 0 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0)
        {
            humandata = new HumanData(position, rotation, this.NowTime, this.AfterAccident , ovrcamera.GetComponent<LegacyAccidentPresentation>().AccidentProgress);
        }
        else
        {
            humandata = new HumanData(ovrcamera.transform.position, ovrcamera.transform.eulerAngles, this.NowTime, this.AfterAccident, ovrcamera.GetComponent<LegacyAccidentPresentation>().AccidentProgress);
        }
        HumanDataList.Add(humandata);
    }

    //全てのデータをCSVに書き出し
    public void CSVPrint()
    {
        // several legacy and current paths end a trial; write each trial once
        if (saved)
            return;
        saved = true;
        //事故フラグとゴールフラグを更新
        OnAccident = ovrcamera.GetComponent<LegacyAccidentPresentation>().Accident;
        OnGoal = gamedirector.GoalFlag;

        //ここにCSV書き出し処理を書く
        //Debug.Log("書き出し");
        //車と人のデータをcsvに書き出し
        LogSave();

        //プリント
        Debug.Log("出力");
    }

    //ゲームディレクターから受け取るための関数
    IEnumerator VariableReceive()
    {
        //少し遅延
        yield return null;

        //人の情報の変数をゲームディレクターから受け取り
        EventNumber = gamedirector.EventNumber;
        Height = gamedirector.Height;
        Weight = gamedirector.Weight;
        DieFlash = gamedirector.DieFlashNumber;
        Gender = gamedirector.Gender;
        Age = gamedirector.Age;
        License = gamedirector.License;
        Hz = gamedirector.Hz;
        SmartPhone = gamedirector.SmartPhone;
        Incident = gamedirector.Incident;
        Weather = gamedirector.Weather;
        SkyTime = gamedirector.SkyTime;

    }

    // Full-GUID session tag shared by the three files of one LogSave call.
    static string NewFileTag() =>
        DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");

    // Atomic no-overwrite creation: throws IOException instead of reusing an
    // existing experiment file.
    static StreamWriter CreateNewCsvWriter(string path) =>
        new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None));

    // Opens the three CSV writers of one save round. All share one fileTag so
    // Car/Human/InExperiment files stay correlatable. Retries with a fresh tag
    // on the astronomically unlikely GUID collision; never appends to another round.
    static string[] lastPaths = new string[0];

    static (StreamWriter car, StreamWriter human, StreamWriter inExperiment) CreateCsvWriters()
    {
        for (int attempt = 0; ; attempt++)
        {
            string fileTag = NewFileTag();
            string carPath = Path.Combine(Application.persistentDataPath, $"CarData_{fileTag}.csv");
            string humanPath = Path.Combine(Application.persistentDataPath, $"HumanData_{fileTag}.csv");
            string inExperimentPath = Path.Combine(Application.persistentDataPath, $"HumanData_InExperiment_{fileTag}.csv");
            StreamWriter car = null;
            StreamWriter human = null;
            StreamWriter inExperiment = null;
            try
            {
                car = CreateNewCsvWriter(carPath);
                human = CreateNewCsvWriter(humanPath);
                inExperiment = CreateNewCsvWriter(inExperimentPath);
                lastPaths = new[] { carPath, humanPath, inExperimentPath };
                return (car, human, inExperiment);
            }
            catch (IOException)
            {
                car?.Dispose();
                human?.Dispose();
                inExperiment?.Dispose();
                if (attempt >= 2)
                    throw;
            }
        }
    }

    //データをcsvに書き出すメソッド
    public void LogSave()
    {
        //テキストの書き込み準備（毎回新規作成し、他輪データへの追記はしない）
        //using宣言により、書き込み中の例外時にも確実にFlush/Disposeされる
        var writers = CreateCsvWriters();
        LastWrittenFiles = lastPaths;
        using StreamWriter streamwriter_CarData = writers.car;
        using StreamWriter streamwriter_HumanData = writers.human;
        using StreamWriter streamwriter_HumanData_InExperiment = writers.inExperiment;

        //被験者の情報データを書き出し
        //1行目部分
        string[] humandata_head = {"EventNumber",
        "Height",
        "Weight",
        "DieFlash",
        "Gender",
        "Age",
        "License",
        "OnAcident",
        "OnGoal",
        "Hz",
        "SmartPhone",
        "Incident",
        "Weather",
        "SkyTime",
        "Scene",
        "CustomScenario"};
        streamwriter_HumanData.WriteLine(string.Join(",", humandata_head));
        //2行目部分
        string[] humandata_string = {EventNumber.ToString(),
            Height.ToString(),
            Weight.ToString(),
            DieFlash.ToString(),
            Gender.ToString(),
            Age.ToString(),
            License.ToString(),
            OnAccident.ToString(),
            OnGoal.ToString(),
            F(Hz),
            SmartPhone.ToString(),
            Incident.ToString(),
            Weather.ToString(),
            SkyTime.ToString(),
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            // id of the JSON scenario when EventNumber is CustomScenarioSession.EventNumber (100)
            CustomScenarioSession.IsCustom(EventNumber) && CustomScenarioSession.Current != null
                ? CustomScenarioSession.Current.id : string.Empty};
        //人のデータを","で連結して書き込む
        streamwriter_HumanData.WriteLine(string.Join("," , humandata_string));

        //人の実験中のデータを書き出し
        //1行目部分
        string[] humandata_InExperiment_head = {"Time",
        "PlayerPositionX",
        "PlayerPositionY",
        "PlayerPositionZ",
        "PlayerRotationX",
        "PlayerRotationY",
        "PlayerRotationZ",
        "AfterAcident",
        "AcidentProgress"
        };
        streamwriter_HumanData_InExperiment.WriteLine(string.Join(",", humandata_InExperiment_head));
        //2行目以降部分
        string[] humandata_InExperiment_string = new string[9];
        foreach (HumanData human in HumanDataList)
        {
            humandata_InExperiment_string[0] = F(human.HumanTime);
            humandata_InExperiment_string[1] = F(human.HumanPosition.x);
            humandata_InExperiment_string[2] = F(human.HumanPosition.y);
            humandata_InExperiment_string[3] = F(human.HumanPosition.z);
            humandata_InExperiment_string[4] = F(human.HumanRotation.x);
            humandata_InExperiment_string[5] = F(human.HumanRotation.y);
            humandata_InExperiment_string[6] = F(human.HumanRotation.z);
            humandata_InExperiment_string[7] = human.AfterAccident.ToString();
            humandata_InExperiment_string[8] = human.AccidentProgress.ToString();

            //人の実験中のデータを","で連結して書き込む
            streamwriter_HumanData_InExperiment.WriteLine(string.Join(",", humandata_InExperiment_string));
        }

        //車のデータを書き出し
        //1行目部分
        string[] cardata_head = {"Time",
        "CarID",
        "CarPositionX",
        "CarPositionY",
        "CarPositionZ",
        "AcidentCar"
        };
        streamwriter_CarData.WriteLine(string.Join(",", cardata_head));
        //2行目以降部分
        string[] cardata_string = new string[6];
        foreach (CarData car in CarDataList)
        {
            cardata_string[0] = F(car.CarTime);
            cardata_string[1] = car.CarID.ToString();
            cardata_string[2] = F(car.CarPosition.x);
            cardata_string[3] = F(car.CarPosition.y);
            cardata_string[4] = F(car.CarPosition.z);
            cardata_string[5] = car.AccidentCar.ToString();

            //車のデータを","で連結して書き込む
            streamwriter_CarData.WriteLine(string.Join(",", cardata_string));
        }

        //以下でcsv出力
        streamwriter_CarData.Flush();
        streamwriter_CarData.Close();

        streamwriter_HumanData.Flush();
        streamwriter_HumanData.Close();

        streamwriter_HumanData_InExperiment.Flush();
        streamwriter_HumanData_InExperiment.Close();
    }

    // Start is called before the first frame update
    void Start()
    {
        //時間を初期化
        NowTime = 0;

        //フラグを初期化
        OnAccident = 0;
        OnGoal = 0;
        AfterAccident = 0;

        //ゲームオブジェクトを格納
        this.ovrcamera = OpenXRScene.CameraControllerObject;

        //スクリプトを格納
        this.gamedirector = this.gameObject.GetComponent<GameDirector>();

        //ゲームディレクターから変数を受け取る
        StartCoroutine(VariableReceive());

        //人と車のデータリスト
        this.CarDataList = new List<CarData>();
        this.HumanDataList = new List<HumanData>();

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //時間を進める
        NowTime += Time.deltaTime;
    }
}
