using UnityEngine;
using UnityEngine.UI;

public class ButtonTest : MonoBehaviour
{
    //ボタンを格納
    Button button;
    public ParticleSystem Effect;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => PlayEffect());

    }

    public void PlayEffect()
    {
        Effect.Play();
        Debug.Log("押した");
    }
}