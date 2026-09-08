using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccidentCarFactory : MonoBehaviour
{
    //ゲームオブジェクトを格納する変数
    [SerializeField] GameObject carfactory_Left;
    [SerializeField] GameObject carfactory_Right;
    [SerializeField] GameObject carfactory_Left2;
    [SerializeField] GameObject carfactory_Right2;
    [SerializeField] GameObject gamedirector;

    //イベントナンバーを格納する変数
    int EventNumber;

    //事故車生産フラグ
    [SerializeField] bool AcidentPrediction;

    void OnTriggerEnter(Collider collider)
    {
        if (TrafficAccidentState.IsFrozen)
            return;

        //人が入った
        var isTrackedPlayer = PlayerActor.TryResolve(collider, out _);
        var isLegacyHuman = collider.CompareTag("Human") && collider.name == "Human";
        if ((isTrackedPlayer || isLegacyHuman) && !AcidentPrediction)
        {
            //Debug.Log("生産");
            //イベントナンバー0
            if (EventNumber == 0)
            {
                AcidentPrediction = true;

                //車の生産を停止
                carfactory_Right.GetComponent<CarFactory>().AcidentCarChange();
                //carfactory_Left.GetComponent<CarFactory>().AcidentCarChange();

                //車を生産
                carfactory_Right.GetComponent<CarFactory>().CoroutineStart();
            }
            if(EventNumber == 1)
            {
                AcidentPrediction = true;

                //車の生産を停止
                //carfactory_Right2.GetComponent<CarFactory>().AcidentCarChange();
                carfactory_Left2.GetComponent<CarFactory>().AcidentCarChange();

                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();

                //2台目を生産
                StartCoroutine("AcidentCarSecond");
            }
            if (EventNumber == 2)
            {
                AcidentPrediction = true;

                //車の生産を停止
                //carfactory_Right.GetComponent<CarFactory>().AcidentCarChange();

                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();

                //2台目を生産
                StartCoroutine("AcidentCarSecond");
            }
            if (EventNumber == 3 || EventNumber == 5)
            {
                AcidentPrediction = true;

                //車の生産を停止
                //carfactory_Right.GetComponent<CarFactory>().AcidentCarChange();

                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();

                //2台目を生産
                StartCoroutine("AcidentCarSecond");
            }
            if (EventNumber == 6 || EventNumber == 7)
            {
                AcidentPrediction = true;
                
                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;
                   
                //工場を再設定
                carfactory_Left = AcidentAreas.transform.Find("CarFactory_Acident_Left").gameObject;
                carfactory_Right = AcidentAreas.transform.Find("CarFactory_Acident_Right").gameObject;

                //車の生産を停止
                carfactory_Right.GetComponent<CarFactory>().AcidentCarChange();
                //carfactory_Left.GetComponent<CarFactory>().AcidentCarChange();

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();
            }
            if (EventNumber == 4)
            {
                AcidentPrediction = true;

                //車の生産を停止
                carfactory_Right2.GetComponent<CarFactory>().AcidentCarChange();
                //carfactory_Left2.GetComponent<CarFactory>().AcidentCarChange();

                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();

                //2台目を生産
                StartCoroutine("AcidentCarSecond");

            }
            if (EventNumber == 8)
            {
                AcidentPrediction = true;

                //車の生産を停止
                //carfactory_Right.GetComponent<CarFactory>().AcidentCarChange();

                //アクシデントエリアの親オブジェクトを取得
                GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

                //車を生産
                AcidentAreas.transform.Find("CarFactory_Acident_Right").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();
                AcidentAreas.transform.Find("CarFactory_Acident_Left").gameObject.
                    GetComponent<CarFactory>().CoroutineStart();

                //2台目を生産
                StartCoroutine("AcidentCarSecond");
            }
        }
    }

    //シナリオ2,3,5用の遅延事故車
    IEnumerator AcidentCarSecond()
    {
        if (TrafficAccidentState.IsFrozen)
            yield break;

        //アクシデントエリアの親オブジェクトを取得
        GameObject AcidentAreas = this.gameObject.transform.parent.gameObject;

        if (EventNumber == 2)
        {
            //遅延させて発射
            yield return null;
            //車を生産
            AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                GetComponent<CarFactory>().CoroutineStart();
        }
        else if (EventNumber == 1 ||EventNumber == 3 || EventNumber == 4 || EventNumber == 5) 
        {
            //遅延させて発射
            yield return new WaitForSeconds(3.5f);
            //車を生産
            AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                GetComponent<CarFactory>().CoroutineStart();
        }
        else if (EventNumber == 8)
        {

            //右から遅延させて発射
            yield return new WaitForSeconds(12f);
            //車を生産
            AcidentAreas.transform.Find("CarFactory_Acident_Right").gameObject.
                GetComponent<CarFactory>().CoroutineStart();
        }


        

        //遅延させて3台目を発射
        if (EventNumber == 2)
        {
            //遅延させて発射
            yield return new WaitForSeconds(3.5f);
            //もう一つ車を生産
            AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                GetComponent<CarFactory>().CoroutineStart();
        }
        else if(EventNumber == 3 || EventNumber == 5)
        {
            //遅延させて発射
            yield return new WaitForSeconds(7f);
            //もう一つ車を生産
            AcidentAreas.transform.Find("CarFactory_Acident").gameObject.
                GetComponent<CarFactory>().CoroutineStart();
        }

        
    }

    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        var context = GameplaySceneContext.Instance;
        if (context != null)
        {
            carfactory_Left = context.LeftFactory != null ? context.LeftFactory.gameObject : null;
            carfactory_Right = context.RightFactory != null ? context.RightFactory.gameObject : null;
            carfactory_Left2 = context.LeftFactory2 != null ? context.LeftFactory2.gameObject : null;
            carfactory_Right2 = context.RightFactory2 != null ? context.RightFactory2.gameObject : null;
            gamedirector = context.Director != null ? context.Director.gameObject : null;
        }
        if (gamedirector == null)
            gamedirector = FindFirstObjectByType<GameDirector>()?.gameObject;

        

        //イベントナンバーを格納
        if (gamedirector == null)
        {
            Debug.LogError("AccidentCarFactory requires a GameDirector.", this);
            enabled = false;
            return;
        }
        EventNumber = gamedirector.GetComponent<GameDirector>().EventNumber;

        AcidentPrediction = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
