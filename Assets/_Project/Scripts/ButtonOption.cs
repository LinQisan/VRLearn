using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ButtonOption : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    GameObject gamedirector;

    //引き継ぐ変数
    int SaveNumber;
    int Height;
    int Weight;
    int DieFlashNumber;
    int Gender;
    int Age;
    int License;
    float Hz;
    int SmartPhone;
    int Incident;
    int Weather;
    int SkyTime;

    //ボタンを格納する変数
    Button button;

    //サウンドテスト用変数(インスペクターから数値を指定,Button_Hz以外のボタンには用いない)
    public float HzTestValue;

    //車が動くボタン
    public void CarStart()
    {
        gamedirector.GetComponent<GameDirector>().CarAutoMove();
    }

    //Canvasをオンオフするボタン
    public void CanvasOnOffCall()
    {
        gamedirector.GetComponent<GameDirector>().CanvasOnOff();
    }

    public void CanvasOnOffCall_Title()
    {
        gamedirector.GetComponent<GameDirector_Title>().CanvasOnOff();
    }

    //リセットボタン
    public void Reset()
    {
        //時間をもとに戻す
        Time.timeScale = 1f;

        //シーンを引き継ぐ変数
        //gamedirector.GetComponent<GameDirector_Title>().EventNumber = 100;

        //変数登録
        SaveNumber = gamedirector.GetComponent<GameDirector>().EventNumber;
        DieFlashNumber = gamedirector.GetComponent<GameDirector>().DieFlashNumber;
        Height = gamedirector.GetComponent<GameDirector>().Height;
        Weight = gamedirector.GetComponent<GameDirector>().Weight;
        Gender = gamedirector.GetComponent<GameDirector>().Gender;
        Age = gamedirector.GetComponent<GameDirector>().Age;
        License = gamedirector.GetComponent<GameDirector>().License;
        SmartPhone = gamedirector.GetComponent<GameDirector>().SmartPhone;
        Incident = gamedirector.GetComponent<GameDirector>().Incident;
        Weather = gamedirector.GetComponent<GameDirector>().Weather;
        SkyTime = gamedirector.GetComponent<GameDirector>().SkyTime;

        //ランダムにイベントを選ぶ
        if (SaveNumber == SceneRoute.RandomScenarioId)
        {
            SaveNumber = SceneRoute.DrawRandomScenarioId();

        }
        //シーン遷移時イベント
        SceneManager.sceneLoaded += VariantSave_Reset;

        //シーン切り替え
        SceneManager.LoadScene(SceneRoute.CurrentScene);
    }

    //ゲームスタートボタン
    public void GameStart()
    {
        Debug.Log("ゲームスタート");

        //シーンを引き継ぐ変数
        //gamedirector.GetComponent<GameDirector_Title>().EventNumber = 100;

        //変数登録
        SaveNumber = gamedirector.GetComponent<GameDirector_Title>().EventNumber;
        DieFlashNumber = gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber;
        Height = gamedirector.GetComponent<GameDirector_Title>().Height;
        Weight = gamedirector.GetComponent<GameDirector_Title>().Weight;
        Gender = gamedirector.GetComponent<GameDirector_Title>().Gender;
        Age = gamedirector.GetComponent<GameDirector_Title>().Age;
        License = gamedirector.GetComponent<GameDirector_Title>().License;
        Hz = gamedirector.GetComponent<GameDirector_Title>().Hz;
        SmartPhone = gamedirector.GetComponent<GameDirector_Title>().SmartPhone;
        Incident = gamedirector.GetComponent<GameDirector_Title>().Incident;
        Weather = gamedirector.GetComponent<GameDirector_Title>().Weather;
        SkyTime = gamedirector.GetComponent<GameDirector_Title>().SkyTime;

        //ランダムにイベントを選ぶ
        if (SaveNumber == SceneRoute.RandomScenarioId)
        {
            SaveNumber = SceneRoute.DrawRandomScenarioId();

        }
        //シーン遷移時イベント
        SceneManager.sceneLoaded += VariantSave;

        //シーン切り替え
        SceneManager.LoadScene(SceneRoute.GameplayForCurrentScene);
    }

    //走馬灯オンオフボタン
    public void DieFlashOnOff()
    {
        if (gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber == 0) 
        {
            gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber = 1;
            
        }
        else
        {
            gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber = 0;
            
        }
        if (gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber == 1)
        {
            this.gameObject.transform.Find("DieFlashOnOff").gameObject.GetComponent<Text>().text =
                "DieFlashOn";
        }
        else
        {
            this.gameObject.transform.Find("DieFlashOnOff").gameObject.GetComponent<Text>().text =
                "DieFlashOff";
        }
        //GameObject.Find("GameDirector_Title").GetComponent<GameDirector_Title>().DieFlash = this.DieFlash;
    }

    //スコア表示ボタン
    public void ShowScore()
    {
        this.gameObject.transform.Find("ShowScore").gameObject.GetComponent<Text>().text = 
            gamedirector.GetComponent<GameDirector>().DieFlashNumber.ToString();

    }

    //イベントナンバーの上下ボタン
    public void UpNumber()
    {
        gamedirector.GetComponent<GameDirector_Title>().EventNumber =
            SceneRoute.StepScenarioSelection(gamedirector.GetComponent<GameDirector_Title>().EventNumber, 1);
    }
    public void DownNumber()
    {
        gamedirector.GetComponent<GameDirector_Title>().EventNumber =
            SceneRoute.StepScenarioSelection(gamedirector.GetComponent<GameDirector_Title>().EventNumber, -1);
    }

    //身長の上下ボタン
    public void HeightUp()
    {
        gamedirector.GetComponent<GameDirector_Title>().Height += 5;
    }
    public void HeightDown()
    {
        gamedirector.GetComponent<GameDirector_Title>().Height -= 5;
    }

    //体重の上下ボタン
    public void WeightUp()
    {
        gamedirector.GetComponent<GameDirector_Title>().Weight += 5;
    }
    public void WeightDown()
    {
        gamedirector.GetComponent<GameDirector_Title>().Weight -= 5;
    }

    //タイトルに戻るボタン
    public void BackTitle()
    {
        //時間をもとに戻す
        Time.timeScale = 1f;

        //シーンを引き継ぐ変数
        //gamedirector.GetComponent<GameDirector_Title>().EventNumber = 100;

        //変数登録
        SaveNumber = gamedirector.GetComponent<GameDirector>().EventNumber;
        DieFlashNumber = gamedirector.GetComponent<GameDirector>().DieFlashNumber;
        Height = gamedirector.GetComponent<GameDirector>().Height;
        Weight = gamedirector.GetComponent<GameDirector>().Weight;
        Gender = gamedirector.GetComponent<GameDirector>().Gender;
        Age = gamedirector.GetComponent<GameDirector>().Age;
        License = gamedirector.GetComponent<GameDirector>().License;
        Hz = gamedirector.GetComponent<GameDirector>().Hz;
        SmartPhone = gamedirector.GetComponent<GameDirector>().SmartPhone;
        Incident = gamedirector.GetComponent<GameDirector>().Incident;
        Weather = gamedirector.GetComponent<GameDirector>().Weather;
        SkyTime = gamedirector.GetComponent<GameDirector>().SkyTime;

        //ランダムにイベントを選ぶ
        if (SaveNumber == SceneRoute.RandomScenarioId)
        {
            SaveNumber = SceneRoute.DrawRandomScenarioId();

        }
        //コントローラーを振動を止める
        OpenXRInput.StopControllerVibration();
        //シーン遷移時イベント
        SceneManager.sceneLoaded += VariantSave_ToTitle;

        //シーン切り替え
        SceneManager.LoadScene(SceneRoute.TitleForCurrentScene);
    }

    //性別選択
    public void GenderChange()
    {
        gamedirector.GetComponent<GameDirector_Title>().Gender += 1;
        if(gamedirector.GetComponent<GameDirector_Title>().Gender >= 3)
        {
            gamedirector.GetComponent<GameDirector_Title>().Gender = 0;
        }
    }

    //年齢選択
    public void AgeUp()
    {
        gamedirector.GetComponent<GameDirector_Title>().Age += 5;
    }
    public void AgeDown()
    {
        gamedirector.GetComponent<GameDirector_Title>().Age -= 5;
    }

    //走馬灯オンオフボタン
    public void LicenseOnOff()
    {
        if (gamedirector.GetComponent<GameDirector_Title>().License == 0)
        {
            gamedirector.GetComponent<GameDirector_Title>().License = 1;

        }
        else
        {
            gamedirector.GetComponent<GameDirector_Title>().License = 0;

        }
        if (gamedirector.GetComponent<GameDirector_Title>().License == 1)
        {
            this.gameObject.transform.Find("License").gameObject.GetComponent<Text>().text =
                "HaveLicense";
        }
        else
        {
            this.gameObject.transform.Find("License").gameObject.GetComponent<Text>().text =
                "NoLicense";
        }
        //GameObject.Find("GameDirector_Title").GetComponent<GameDirector_Title>().DieFlash = this.DieFlash;
    }

    //音を上げ下げ
    public void HzUp()
    {

        gamedirector.GetComponent<GameDirector_Title>().Sound += 1000;
        //centereyeに反映
        if (gamedirector.GetComponent<GameDirector_Title>().Sound <= 16000)
        {
            gamedirector.GetComponent<GameDirector_Title>().SoundTest();
        }
    }
    public void HzDown()
    {
        gamedirector.GetComponent<GameDirector_Title>().Sound -= 1000;
        if (gamedirector.GetComponent<GameDirector_Title>().Sound >= 5000)
        {
            //centereyeに反映
            gamedirector.GetComponent<GameDirector_Title>().SoundTest();
        }
        

        
    }

    //スマホオンオフ
    public void SmartPhoneOnOff()
    {
        if (gamedirector.GetComponent<GameDirector_Title>().SmartPhone == 0)
        {
            gamedirector.GetComponent<GameDirector_Title>().SmartPhone = 1;

        }
        else
        {
            gamedirector.GetComponent<GameDirector_Title>().SmartPhone = 0;

        }
    }

    //性別変更改
    public void MaleOn()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Gender = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_Female").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_Unknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void FemaleOn()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Gender = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_Male").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_Unknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void GenderUnknownOn()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Gender = 2;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_Female").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_Male").GetComponent<Image>().color = ButtonColorGrey;
    }

    //免許切り替えボタン改
    public void LicenseHave()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().License = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_NoHave").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_LicenseUnknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void LicenseNoHave()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().License = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_Have").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_LicenseUnknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void LicenseUnknown()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().License = 2;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_Have").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_NoHave").GetComponent<Image>().color = ButtonColorGrey;
    }

    //事故経験ボタン
    public void IncidentYes()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Incident = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_NoIncident").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_IncidentUnknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void IncidentNo()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Incident = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_YesIncident").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_IncidentUnknown").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void IncidentUnknown()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Incident = 2;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_NoIncident").GetComponent<Image>().color = ButtonColorGrey;
        GameObject.Find("Button_YesIncident").GetComponent<Image>().color = ButtonColorGrey;
    }

    //走馬灯切り替えボタン改
    public void DieFlashOn()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_DieFlashOff").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void DieFlashOff()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().DieFlashNumber = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_DieFlashOn").GetComponent<Image>().color = ButtonColorGrey;
    }

    //歩きスマホボタン改
    public void SmartPhoneOn()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().SmartPhone = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_SmartPhoneOff").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void SmartPhoneOff()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().SmartPhone = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_SmartPhoneOn").GetComponent<Image>().color = ButtonColorGrey;
    }

    //天候ボタン
    public void WeatherClear()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Weather = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_WeatherRain").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void WeatherRain()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().Weather = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_WeatherClear").GetComponent<Image>().color = ButtonColorGrey;
    }

    //時間ボタン
    public void TimeDay()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().SkyTime = 0;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_TimeNight").GetComponent<Image>().color = ButtonColorGrey;
    }
    public void TimeNight()
    {
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        gamedirector.GetComponent<GameDirector_Title>().SkyTime = 1;
        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;
        GameObject.Find("Button_TimeDay").GetComponent<Image>().color = ButtonColorGrey;
    }

    //事故詳細を閉じるボタン
    public void AcidentDetailClose()
    {
        //メソッドを起動
        OpenXRScene.CameraControllerObject.GetComponent<CenterEyeCamera>().AcidentClose();
    }

    //音声テストボタン
    public void HzTest()
    {
        //色を格納
        Color ButtonColorWhite;
        Color ButtonColorGrey;

        ColorUtility.TryParseHtmlString("#ffffff", out ButtonColorWhite);
        ColorUtility.TryParseHtmlString("#d3d3d3", out ButtonColorGrey);

        //インスペクターに設定された変数をそのままHzにする
        gamedirector.GetComponent<GameDirector_Title>().Hz = HzTestValue;
        //自分を光らせ、他を暗くする
        gamedirector.GetComponent<GameDirector_Title>().SoundButtonOff();
        this.gameObject.GetComponent<Image>().color = ButtonColorWhite;

        //音を鳴らす
        gamedirector.GetComponent<GameDirector_Title>().SoundTest();
    }

    //録画開始ボタン
    public void RecordStart()
    {
        gamedirector.GetComponent<CameraRecorder>().StartRecording();
    }
    //録画終了ボタン
    public void RecordEnd()
    {
        gamedirector.GetComponent<CameraRecorder>().StopRecording();
    }

    //ゲームがスタートすると動く
    void Start()
    {
        //ゲームオブジェクトを格納
        this.gamedirector = GameObject.Find("GameDirector");

        //ボタンを格納後、押下時のイベントを追加
        button = GetComponent<Button>();

        //車を動かすボタン
        if (this.gameObject.tag == "CarStart")
        {
            button.onClick.AddListener(() => CarStart());
        }

        //Canvasをオンオフするボタン
        if (this.gameObject.tag == "CanvasOnOff")
        {
            button.onClick.AddListener(() => CanvasOnOffCall());
        }
        if (this.gameObject.tag == "CanvasOnOff_Title")
        {
            button.onClick.AddListener(() => CanvasOnOffCall_Title());
        }

        //リセットボタン
        if (this.gameObject.tag == "Reset")
        {
            button .onClick.AddListener(() => Reset());
        }

        //ゲームスタートボタン
        if (this.gameObject.tag == "GameStart")
        {
            button.onClick.AddListener(() => GameStart());
        }

        //スコア表示ボタン
        if (this.gameObject.tag == "ShowScore")
        {
            button.onClick.AddListener(() => ShowScore());
        }

        //イベントナンバーの上下ボタン
        if (this.gameObject.tag == "UpNumber")
        {
            button.onClick.AddListener(() => UpNumber());
        }
        if (this.gameObject.tag == "DownNumber")
        {
            button.onClick.AddListener(() => DownNumber());
        }

        //タイトルに戻るボタン
        if (this.gameObject.tag == "BackTitle")
        {
            button.onClick.AddListener(() => BackTitle());
        }

        //録画開始ボタン
        if(this.gameObject.tag == "RecordStart")
        {
            button.onClick.AddListener(() => RecordStart());
        }
        //録画終了ボタン
        if (this.gameObject.tag == "RecordEnd")
        {
            button.onClick.AddListener(() => RecordEnd());
        }

        //走馬灯オンオフボタン
        if (this.gameObject.tag == "DieFlashOnOff")
        {
            //Debug.Log("DieFlash");

            button.onClick.AddListener(() => DieFlashOnOff());
        }

        //身長の上下ボタン
        if (this.gameObject.tag == "HeightUp")
        {
            button.onClick.AddListener(() => HeightUp());
        }
        if (this.gameObject.tag == "HeightDown")
        {
            button.onClick.AddListener(() => HeightDown());
        }

        //体重の上下ボタン
        if (this.gameObject.tag == "WeightUp")
        {
            button.onClick.AddListener(() => WeightUp());
        }
        if (this.gameObject.tag == "WeightDown")
        {
            button.onClick.AddListener(() => WeightDown());
        }

        //性別選択ボタン
        if(this.gameObject.tag == "Gender")
        {
            button.onClick.AddListener(() => GenderChange());
        }

        //体重の上下ボタン
        if (this.gameObject.tag == "AgeUp")
        {
            button.onClick.AddListener(() => AgeUp());
        }
        if (this.gameObject.tag == "AgeDown")
        {
            button.onClick.AddListener(() => AgeDown());
        }

        //運転免許ボタン
        if (this.gameObject.tag == "License")
        {
            button.onClick.AddListener(() => LicenseOnOff());
        }

        //年齢上げ下げボタン
        if (this.gameObject.tag == "HzUp")
        {
            button.onClick.AddListener(() => HzUp());
        }
        if (this.gameObject.tag == "HzDown")
        {
            button.onClick.AddListener(() => HzDown());
        }

        //スマホ切り替えボタン
        if(this.gameObject.tag == "SmartPhone")
        {
            button.onClick.AddListener(() => SmartPhoneOnOff());
        }

        //性別ボタン改
        if(this.gameObject.tag == "Male")
        {
            button.onClick.AddListener(() => MaleOn());
        }
        if (this.gameObject.tag == "Female")
        {
            button.onClick.AddListener(() => FemaleOn());
        }
        if (this.gameObject.tag == "GenderUnknown")
        {
            button.onClick.AddListener(() => GenderUnknownOn());
        }

        //免許ボタン改
        if (this.gameObject.tag == "HaveLicense")
        {
            button.onClick.AddListener(() => LicenseHave());
        }
        if (this.gameObject.tag == "NoLicense")
        {
            button.onClick.AddListener(() => LicenseNoHave());
        }
        if (this.gameObject.tag == "LicenseUnknown")
        {
            button.onClick.AddListener(() => LicenseUnknown());
        }

        //事故経験ボタン
        if (this.gameObject.tag == "YesIncident")
        {
            button.onClick.AddListener(() => IncidentYes());
        }
        if (this.gameObject.tag == "NoIncident")
        {
            button.onClick.AddListener(() => IncidentNo());
        }
        if (this.gameObject.tag == "IncidentUnknown")
        {
            button.onClick.AddListener(() => IncidentUnknown());
        }

        //走馬灯ボタン改
        if (this.gameObject.tag == "DieFlashOn")
        {
            button.onClick.AddListener(() => DieFlashOn());
        }
        if (this.gameObject.tag == "DieFlashOff")
        {
            button.onClick.AddListener(() => DieFlashOff());
        }

        //歩きスマホボタン改
        if (this.gameObject.tag == "SmartPhoneOn")
        {
            button.onClick.AddListener(() => SmartPhoneOn());
        }
        if (this.gameObject.tag == "SmartPhoneOff")
        {
            button.onClick.AddListener(() => SmartPhoneOff());
        }

        //天候ボタン
        if (this.gameObject.tag == "WeatherClear")
        {
            button.onClick.AddListener(() => WeatherClear());
        }
        if (this.gameObject.tag == "WeatherRain")
        {
            button.onClick.AddListener(() => WeatherRain());
        }

        //時間ボタン
        if (this.gameObject.tag == "TimeDay")
        {
            button.onClick.AddListener(() => TimeDay());
        }
        if (this.gameObject.tag == "TimeNight")
        {
            button.onClick.AddListener(() => TimeNight());
        }

        //事故スキップボタン
        if(this.gameObject.tag == "AcidentDetailClose")
        {
            button.onClick.AddListener(() => AcidentDetailClose());
        }

        //音声テストボタン
        if(this.gameObject.tag == "HzTest")
        {
            button.onClick.AddListener(() => HzTest());
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //シーン切り替え時イベント
    private void VariantSave(Scene next, LoadSceneMode mode)
    {
        // シーン切り替え後のスクリプトを取得
        var VariantSaver = GameObject.Find("GameDirector").GetComponent<GameDirector>();

        // データを渡す処理
        VariantSaver.EventNumber = SaveNumber;
        VariantSaver.DieFlashNumber = DieFlashNumber;
        VariantSaver.Height = Height;
        VariantSaver.Weight = Weight;
        VariantSaver.Gender = Gender;
        VariantSaver.Age = Age;
        VariantSaver.License = License;
        VariantSaver.Hz = Hz;
        VariantSaver.SmartPhone = SmartPhone;
        VariantSaver.Incident = Incident;
        VariantSaver.Weather = Weather;
        VariantSaver.SkyTime = SkyTime;

        // イベントから削除
        SceneManager.sceneLoaded -= VariantSave;
    }
    //タイトルに戻る時のイベント
    private void VariantSave_ToTitle(Scene next, LoadSceneMode mode)
    {
        // シーン切り替え後のスクリプトを取得
        var VariantSaver = GameObject.Find("GameDirector").GetComponent<GameDirector_Title>();

        // データを渡す処理
        VariantSaver.EventNumber = SaveNumber;
        VariantSaver.DieFlashNumber = DieFlashNumber;
        VariantSaver.Height = Height;
        VariantSaver.Weight = Weight;
        VariantSaver.Gender = Gender;
        VariantSaver.Age = Age;
        VariantSaver.License = License;
        VariantSaver.Hz = Hz;
        VariantSaver.SmartPhone = SmartPhone;
        VariantSaver.Incident = Incident;
        VariantSaver.Weather = Weather;
        VariantSaver.SkyTime = SkyTime;

        // イベントから削除
        SceneManager.sceneLoaded -= VariantSave_ToTitle;
    }
    //リセットする時のイベント
    private void VariantSave_Reset(Scene next, LoadSceneMode mode)
    {
        // シーン切り替え後のスクリプトを取得
        var VariantSaver = GameObject.Find("GameDirector").GetComponent<GameDirector>();

        // データを渡す処理
        VariantSaver.EventNumber = SaveNumber;
        VariantSaver.DieFlashNumber = DieFlashNumber;
        VariantSaver.Height = Height;
        VariantSaver.Weight = Weight;
        VariantSaver.Gender = Gender;
        VariantSaver.Age = Age;
        VariantSaver.License = License;
        VariantSaver.Hz = Hz;
        VariantSaver.SmartPhone = SmartPhone;
        VariantSaver.Incident = Incident;
        VariantSaver.Weather = Weather;
        VariantSaver.SkyTime = SkyTime;

        // イベントから削除
        SceneManager.sceneLoaded -= VariantSave_Reset;
    }

}
