using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-950)]
public class GameDirector_Title : MonoBehaviour
{
    public event System.Action UIStateChanged;

    [Header("Scene references")]
    [SerializeField] Canvas canvasMain;
    [SerializeField] GameObject buttonGroupMain;
    [SerializeField] CenterEyeCamera centerEye;
    [SerializeField] TitleSoundPreview soundPreview;

    [Header("Value labels")]
    [SerializeField] TMP_Text eventNumberText;
    [SerializeField] TMP_Text heightText;
    [SerializeField] TMP_Text weightText;
    [SerializeField] TMP_Text ageText;

    // Built once from the menu hierarchy. Keeping the same controls in both the
    // scene YAML and a runtime cache created two sources of truth.
    TitleOptionToggle[] optionToggles;
    TitleButtonFeedback[] buttonFeedback;

    // Values transferred to the gameplay scene.
    public int EventNumber;
    public int Height;
    public int Weight;
    public int DieFlashNumber;
    public int Gender;
    public int Age;
    public int License;
    public float Hz;
    public int SmartPhone;
    public int Incident;
    public int Weather;
    public int SkyTime;
    public float Sound;

    int displayedEventNumber = int.MinValue;
    int displayedHeight = int.MinValue;
    int displayedWeight = int.MinValue;
    int displayedAge = int.MinValue;
    int displayedChoiceHash = int.MinValue;
    int lastCommandFrame = -1;
    float nextCommandTime;

    const float CommandDebounceSeconds = 0.06f;

    void Awake()
    {
        RebuildControlCaches();
    }

    void Start()
    {
        if (canvasMain == null || buttonGroupMain == null ||
            eventNumberText == null || heightText == null || weightText == null || ageText == null)
        {
            Debug.LogError("TraficAcidentTitle menu references are incomplete.", this);
            enabled = false;
            return;
        }

        RebuildControlCaches();

        foreach (var feedback in buttonFeedback)
        {
            if (feedback != null)
                feedback.Initialize(canvasMain.gameObject);
        }

        RefreshChangedUI(true);
    }

    void RebuildControlCaches()
    {
        // Toggle OnEnable callbacks may run before Start when the title scene is
        // loaded. Build the cache in Awake and retain this fallback for dynamically
        // activated menu hierarchies.
        if (canvasMain != null)
        {
            optionToggles = canvasMain.GetComponentsInChildren<TitleOptionToggle>(true);
            buttonFeedback = canvasMain.GetComponentsInChildren<TitleButtonFeedback>(true);
        }
        if (soundPreview == null)
            soundPreview = GetComponent<TitleSoundPreview>();
    }

    public void CanvasOnOff()
    {
        buttonGroupMain.SetActive(!buttonGroupMain.activeSelf);
    }

    public void ChangeEventNumber(int delta)
    {
        // RANDOM is a real selectable item before scenario 0. Wrap the eleven
        // choices so the initial RANDOM state also responds to the minus button.
        EventNumber = SceneRoute.StepScenarioSelection(EventNumber, delta);
        displayedEventNumber = int.MinValue;
        RefreshChangedUI(false);
    }

    public void ChangeHeight(int delta)
    {
        Height = ChangeSteppedValue(Height, delta, 120, 210);
        displayedHeight = int.MinValue;
        RefreshChangedUI(false);
    }

    public void ChangeWeight(int delta)
    {
        Weight = ChangeSteppedValue(Weight, delta, 30, 180);
        displayedWeight = int.MinValue;
        RefreshChangedUI(false);
    }

    public void ChangeAge(int delta)
    {
        Age = ChangeSteppedValue(Age, delta, 5, 120);
        displayedAge = int.MinValue;
        RefreshChangedUI(false);
    }

    public void PreviewSound()
    {
        if (Hz <= 0f)
        {
            StopSoundPreview();
            return;
        }

        if (soundPreview != null)
            soundPreview.Preview(Hz);
        else if (centerEye != null)
            centerEye.DieSoundTest(Hz);
    }

    public void StopSoundPreview()
    {
        if (soundPreview != null)
            soundPreview.StopPreview();
    }

    public bool TryAcceptCommand()
    {
        if (lastCommandFrame == Time.frameCount || Time.unscaledTime < nextCommandTime)
            return false;

        lastCommandFrame = Time.frameCount;
        nextCommandTime = Time.unscaledTime + CommandDebounceSeconds;
        return true;
    }

    static int ChangeSteppedValue(int current, int delta, int minimum, int maximum)
    {
        return Mathf.Clamp(current + delta, minimum, maximum);
    }

    // Kept for compatibility with ButtonOption instances outside the migrated title canvas.
    public void SoundTest()
    {
        PreviewSound();
    }
    public void SoundButtonOff() => RefreshChangedUI(true);

    public void ApplyOption(TitleOptionKind kind, int intValue, float floatValue)
    {
        switch (kind)
        {
            case TitleOptionKind.Gender: Gender = intValue; break;
            case TitleOptionKind.License: License = intValue; break;
            case TitleOptionKind.Incident: Incident = intValue; break;
            case TitleOptionKind.DieFlash: DieFlashNumber = intValue; break;
            case TitleOptionKind.SmartPhone: SmartPhone = intValue; break;
            case TitleOptionKind.Weather: Weather = intValue; break;
            case TitleOptionKind.SkyTime: SkyTime = intValue; break;
            case TitleOptionKind.Hz:
                Hz = floatValue;
                Sound = Hz;
                // A short preview confirms the selected square. The dedicated
                // preview button can replay it without changing the choice.
                PreviewSound();
                break;
        }

        RefreshChangedUI(false);
    }

    public void ResetDefaults()
    {
        EventNumber = SceneRoute.RandomScenarioId;
        Height = 170;
        Weight = 70;
        Gender = 0;
        Age = 30;
        License = 0;
        DieFlashNumber = 0;
        Hz = 0f;
        SmartPhone = 0;
        Incident = 0;
        Weather = 0;
        SkyTime = 0;
        Sound = 0f;
        StopSoundPreview();
        RefreshChangedUI(true);
    }

    public void StartGame()
    {
        StopSoundPreview();
        var selectedEvent = EventNumber == SceneRoute.RandomScenarioId ? SceneRoute.DrawRandomScenarioId() : EventNumber;

        SceneManager.sceneLoaded += TransferValuesToGameplay;
        EventNumber = selectedEvent;
        SceneManager.LoadScene(SceneRoute.GameplayForCurrentScene);
    }

    void TransferValuesToGameplay(Scene next, LoadSceneMode mode)
    {
        var destination = FindFirstObjectByType<GameDirector>();
        if (destination == null)
        {
            Debug.LogError($"GameDirector was not found after loading {next.name}.");
            SceneManager.sceneLoaded -= TransferValuesToGameplay;
            return;
        }

        destination.EventNumber = EventNumber;
        destination.DieFlashNumber = DieFlashNumber;
        destination.Height = Height;
        destination.Weight = Weight;
        destination.Gender = Gender;
        destination.Age = Age;
        destination.License = License;
        destination.Hz = Hz;
        destination.SmartPhone = SmartPhone;
        destination.Incident = Incident;
        destination.Weather = Weather;
        destination.SkyTime = SkyTime;
        SceneManager.sceneLoaded -= TransferValuesToGameplay;
    }

    void RefreshChangedUI(bool force)
    {
        var valueChanged = force;
        EventNumber = SceneRoute.ClampScenarioSelection(EventNumber);
        Height = Mathf.Clamp(Height, 120, 210);
        Weight = Mathf.Clamp(Weight, 30, 180);
        Age = Mathf.Clamp(Age, 5, 120);

        if (force || displayedEventNumber != EventNumber)
        {
            valueChanged = true;
            displayedEventNumber = EventNumber;
            eventNumberText.SetText(EventNumber == SceneRoute.RandomScenarioId ? "RANDOM" : EventNumber.ToString());
        }

        if (force || displayedHeight != Height)
        {
            valueChanged = true;
            displayedHeight = Height;
            heightText.text = Height.ToString();
        }

        if (force || displayedWeight != Weight)
        {
            valueChanged = true;
            displayedWeight = Weight;
            weightText.text = Weight.ToString();
        }

        if (force || displayedAge != Age)
        {
            valueChanged = true;
            displayedAge = Age;
            ageText.text = Age.ToString();
        }

        var choiceHash = ComputeChoiceHash();
        if (!force && displayedChoiceHash == choiceHash)
        {
            if (valueChanged)
                UIStateChanged?.Invoke();
            return;
        }

        displayedChoiceHash = choiceHash;
        if (optionToggles == null)
            RebuildControlCaches();
        if (optionToggles == null)
            return;
        foreach (var option in optionToggles)
        {
            if (option != null)
                option.RefreshFromModel();
        }
        UIStateChanged?.Invoke();
    }

    int ComputeChoiceHash()
    {
        unchecked
        {
            var hash = Gender;
            hash = hash * 31 + License;
            hash = hash * 31 + Incident;
            hash = hash * 31 + DieFlashNumber;
            hash = hash * 31 + SmartPhone;
            hash = hash * 31 + Weather;
            hash = hash * 31 + SkyTime;
            hash = hash * 31 + Hz.GetHashCode();
            return hash;
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        Canvas configuredCanvas,
        GameObject configuredButtonGroup,
        CenterEyeCamera configuredCenterEye,
        TMP_Text configuredEventText,
        TMP_Text configuredHeightText,
        TMP_Text configuredWeightText,
        TMP_Text configuredAgeText,
        TitleOptionToggle[] configuredOptions,
        TitleButtonFeedback[] configuredFeedback)
    {
        canvasMain = configuredCanvas;
        buttonGroupMain = configuredButtonGroup;
        centerEye = configuredCenterEye;
        eventNumberText = configuredEventText;
        heightText = configuredHeightText;
        weightText = configuredWeightText;
        ageText = configuredAgeText;
        optionToggles = configuredOptions;
        buttonFeedback = configuredFeedback;
    }

    public void EditorBindSoundPreview(TitleSoundPreview configuredPreview)
    {
        soundPreview = configuredPreview;
    }
#endif
}
