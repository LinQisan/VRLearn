using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class CustomButton : MonoBehaviour,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    //イメージを格納する変数
    [SerializeField] Image image;

    //ゲームオブジェクトを格納する変数
    GameObject gamedirector;

    //Tweenを格納する変数
    Tweener tweener;

    //ポインターが当たった時のイベント
    public void OnPointerEnter(PointerEventData eventData)
    {
        tweener = transform.DOScale(0.95f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true);
        tweener = image.DOFade(0.8f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true);
    }
    //ポインターが離れた時のイベント
    public void OnPointerExit(PointerEventData eventData)
    {
        tweener = transform.DOScale(1.0f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true);
        tweener = image.DOFade(1.0f, 0.24f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    // タップ  
    //public System.Action onClickCallback;
    public void OnPointerClick(PointerEventData eventData)
    {
        //onClickCallback?.Invoke();
        if (this.gameObject.tag == "UpNumber")
        {
            gamedirector.GetComponent<GameDirector_Title>().EventNumber =
                SceneRoute.StepScenarioSelection(gamedirector.GetComponent<GameDirector_Title>().EventNumber, 1);
        }
    }

    // タップダウン  
    public void OnPointerDown(PointerEventData eventData) 
    {
        transform.DOScale(0.95f, 0.24f).SetEase(Ease.OutCubic);
        tweener = image.DOFade(0.8f, 0.24f).SetEase(Ease.OutCubic);
    }
    // タップアップ  
    public void OnPointerUp(PointerEventData eventData) 
    {
        transform.DOScale(1f, 0.24f).SetEase(Ease.OutCubic);
        tweener = image.DOFade(1f, 0.24f).SetEase(Ease.OutCubic);
    }
    
    // Start is called before the first frame update
    void Start()
    {
        //ゲームオブジェクトを格納
        this.gamedirector = GameObject.Find("GameDirector");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //シーン切り替え時に呼ばれる
    void OnDisable()
    {
        DOTween.KillAll();
    }
}
