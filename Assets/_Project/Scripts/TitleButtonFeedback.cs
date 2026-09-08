using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Consistent visual, audio, and haptic feedback for the title scene controls.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleButtonFeedback : MonoBehaviour, IPointerClickHandler
{
    public static readonly Color HoverColor = new Color32(186, 232, 255, 255);
    public static readonly Color PressedColor = new Color32(55, 137, 191, 255);

    static AudioSource sharedAudioSource;
    static AudioClip sharedClickClip;

    Selectable selectable;
    public void Initialize(GameObject audioHost)
    {
        selectable = GetComponent<Selectable>();
        if (selectable == null)
            return;

        ConfigureSelectable(selectable);
        EnsureAudio(audioHost);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (selectable == null || !selectable.IsInteractable() ||
            eventData.button != PointerEventData.InputButton.Left)
            return;

        PlayClickFeedback();
    }

    public static void ConfigureSelectable(Selectable target)
    {
        var colors = target.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = HoverColor;
        colors.pressedColor = PressedColor;
        // Do not give persistent EventSystem focus the same visual weight as
        // the transient XR pointer hover.
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(115, 115, 115, 128);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.07f;
        target.transition = Selectable.Transition.ColorTint;
        target.colors = colors;
    }

    public static void PlayClickFeedback()
    {
        if (sharedAudioSource != null && sharedClickClip != null)
            sharedAudioSource.PlayOneShot(sharedClickClip, 0.18f);
        OpenXRInput.SetControllerVibration(0.08f, 0.04f);
    }

    static void EnsureAudio(GameObject audioHost)
    {
        if (sharedAudioSource == null)
        {
            sharedAudioSource = audioHost.GetComponent<AudioSource>();
            if (sharedAudioSource == null)
                sharedAudioSource = audioHost.AddComponent<AudioSource>();

            sharedAudioSource.playOnAwake = false;
            sharedAudioSource.loop = false;
            sharedAudioSource.spatialBlend = 0f;
        }

        if (sharedClickClip != null)
            return;

        const int sampleRate = 24000;
        const float duration = 0.045f;
        var sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var time = i / (float)sampleRate;
            var envelope = 1f - i / (float)sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * time) * envelope * envelope * 0.35f;
        }

        sharedClickClip = AudioClip.Create("Title UI Click", sampleCount, 1, sampleRate, false);
        sharedClickClip.SetData(samples, 0);
    }
}
