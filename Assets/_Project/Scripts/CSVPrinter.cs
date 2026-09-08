using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

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
    public int OnAcident;
    public int OnGoal;
    int AfterAcident;
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
        public int AcidentCar = 0;
        //車の時間
        public float CarTime = 0;

        //コンストラクタ
        public CarData(Vector3 position, int carid , int acidentcar , float cartime)
        {
            CarPosition = position;
            CarID = carid;
            AcidentCar = acidentcar;
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
        public int AfterAcident = 0;

        //事故過程
        public int AcidentProgress = 0;

        //コンストラクタ
        public HumanData(Vector3 position , Vector3 rotation , float humantime , int afteracident , int acidentprogress)
        {
            HumanPosition =position;
            HumanRotation = rotation;
            HumanTime = humantime;
            AfterAcident = afteracident;
            AcidentProgress = acidentprogress;
        }
        
    }

    //車からデータを受け取り、クラスに保存するメソッド
    public void CarDataReciever(Vector3 position , int carid , int acidentcar)
    {
        cardata = new CarData(position,carid, acidentcar, this.NowTime);
        CarDataList.Add(cardata);
    }

    //人からデータを受け取り、クラスに保存するメソッド
    public void HumanDataReciever(Vector3 position , Vector3 rotation)
    {

        if(ovrcamera.GetComponent<CenterEyeCamera>().Acident == 1)
        {
            AfterAcident = 1;
        }
        if(ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress == 0 && gamedirector.GetComponent<GameDirector>().GoalFlag == 0)
        {
            humandata = new HumanData(position, rotation, this.NowTime, this.AfterAcident , ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress);
        }
        else
        {
            humandata = new HumanData(ovrcamera.transform.position, ovrcamera.transform.eulerAngles, this.NowTime, this.AfterAcident, ovrcamera.GetComponent<CenterEyeCamera>().AcidentProgress);
        }
        HumanDataList.Add(humandata);
    }

    //全てのデータをCSVに書き出し
    public void CSVPrint()
    {
        //事故フラグとゴールフラグを更新
        OnAcident = ovrcamera.GetComponent<CenterEyeCamera>().Acident;
        OnGoal = gamedirector.GoalFlag;

        //ここにCSV書き出し処理を書く
        //Debug.Log("書き出し");
        //車と人のデータをcsvに書き出し
        LogSave();

        //プリント
        Debug.Log("出力");
    }

    //ゲームディレクターから受け取るための関数
    IEnumerator VariableRecieve()
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
        "SkyTime"};
        streamwriter_HumanData.WriteLine(string.Join(",", humandata_head));
        //2行目部分
        string[] humandata_string = {EventNumber.ToString(),
            Height.ToString(),
            Weight.ToString(),
            DieFlash.ToString(),
            Gender.ToString(),
            Age.ToString(),
            License.ToString(),
            OnAcident.ToString(),
            OnGoal.ToString(),
            Hz.ToString(),
            SmartPhone.ToString(),
            Incident.ToString(),
            Weather.ToString(),
            SkyTime.ToString()};
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
            humandata_InExperiment_string[0] = human.HumanTime.ToString();
            humandata_InExperiment_string[1] = human.HumanPosition.x.ToString();
            humandata_InExperiment_string[2] = human.HumanPosition.y.ToString();
            humandata_InExperiment_string[3] = human.HumanPosition.z.ToString();
            humandata_InExperiment_string[4] = human.HumanRotation.x.ToString();
            humandata_InExperiment_string[5] = human.HumanRotation.y.ToString();
            humandata_InExperiment_string[6] = human.HumanRotation.z.ToString();
            humandata_InExperiment_string[7] = human.AfterAcident.ToString();
            humandata_InExperiment_string[8] = human.AcidentProgress.ToString();

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
            cardata_string[0] = car.CarTime.ToString();
            cardata_string[1] = car.CarID.ToString();
            cardata_string[2] = car.CarPosition.x.ToString();
            cardata_string[3] = car.CarPosition.y.ToString();
            cardata_string[4] = car.CarPosition.z.ToString();
            cardata_string[5] = car.AcidentCar.ToString();

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
        OnAcident = 0;
        OnGoal = 0;
        AfterAcident = 0;

        //ゲームオブジェクトを格納
        this.ovrcamera = OpenXRScene.CameraControllerObject;

        //スクリプトを格納
        this.gamedirector = this.gameObject.GetComponent<GameDirector>();

        //ゲームディレクターから変数を受け取る
        StartCoroutine(VariableRecieve());

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
