using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MetaTitleMenuPages : MonoBehaviour
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] GameObject contentRoot;
    [SerializeField] TMP_Text selectedSoundText;

    void OnEnable()
    {
        if (director != null)
            director.UIStateChanged += RefreshReadouts;
    }

    void Start()
    {
        if (contentRoot != null)
            contentRoot.SetActive(true);
        RefreshReadouts();
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.UIStateChanged -= RefreshReadouts;
            director.StopSoundPreview();
        }
    }

    void RefreshReadouts()
    {
        if (director == null)
            return;

        var soundLabel = director.Hz > 0f
            ? $"{director.Hz:0} Hz"
            : "未選択 / None";
        if (selectedSoundText != null)
            selectedSoundText.SetText(soundLabel);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameDirector_Title configuredDirector,
        GameObject configuredContentRoot,
        TMP_Text configuredSelectedSoundText)
    {
        director = configuredDirector;
        contentRoot = configuredContentRoot;
        selectedSoundText = configuredSelectedSoundText;
    }
#endif
}
