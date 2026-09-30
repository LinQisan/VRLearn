using UnityEngine;

/// <summary>Authored scene references shared by runtime systems and spawned vehicles.</summary>
[DefaultExecutionOrder(-1200)]
[DisallowMultipleComponent]
public sealed class GameplaySceneContext : MonoBehaviour
{
    public static GameplaySceneContext Instance { get; private set; }

    [field: SerializeField] public GameDirector Director { get; private set; }
    [field: SerializeField] public GameObject Human { get; private set; }
    [field: SerializeField] public GameObject GameplayCanvas { get; private set; }
    [field: SerializeField] public GameObject SmartPhone { get; private set; }
    [field: SerializeField] public GameObject Bicycle { get; private set; }
    [field: SerializeField] public GameObject BicycleAfterAccident { get; private set; }
    [field: SerializeField] public CarFactory LeftFactory { get; private set; }
    [field: SerializeField] public CarFactory RightFactory { get; private set; }
    [field: SerializeField] public CarFactory LeftFactory2 { get; private set; }
    [field: SerializeField] public CarFactory RightFactory2 { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameDirector director,
        GameObject human,
        GameObject canvas,
        GameObject smartPhone,
        GameObject bicycle,
        GameObject bicycleAfterAccident,
        CarFactory leftFactory,
        CarFactory rightFactory,
        CarFactory leftFactory2,
        CarFactory rightFactory2)
    {
        Director = director;
        Human = human;
        GameplayCanvas = canvas;
        SmartPhone = smartPhone;
        Bicycle = bicycle;
        BicycleAfterAccident = bicycleAfterAccident;
        LeftFactory = leftFactory;
        RightFactory = rightFactory;
        LeftFactory2 = leftFactory2;
        RightFactory2 = rightFactory2;
    }
#endif
}
