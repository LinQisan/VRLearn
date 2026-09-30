using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class TitleSoundPreview : MonoBehaviour
{
    const int SampleRate = 48000;
    const float DurationSeconds = 0.85f;
    const float AttackSeconds = 0.025f;
    const float ReleaseSeconds = 0.14f;
    const float SignalAmplitude = 0.28f;

    AudioSource source;
    AudioClip generatedClip;

    public bool IsPlaying => source != null && source.isPlaying;

    void Awake()
    {
        ConfigureSource();
    }

    public void Preview(float frequency)
    {
        StopPreview();
        if (frequency <= 0f)
            return;

        ConfigureSource();

        var safeFrequency = Mathf.Clamp(frequency, 100f, SampleRate * 0.45f);
        var sampleCount = Mathf.CeilToInt(SampleRate * DurationSeconds);
        var samples = new float[sampleCount];
        var phaseStep = 2f * Mathf.PI * safeFrequency / SampleRate;

        for (var sample = 0; sample < sampleCount; sample++)
        {
            var time = sample / (float)SampleRate;
            var remaining = DurationSeconds - time;
            var envelope = Mathf.Min(
                Mathf.Clamp01(time / AttackSeconds),
                Mathf.Clamp01(remaining / ReleaseSeconds));
            samples[sample] = Mathf.Sin(phaseStep * sample) * envelope * SignalAmplitude;
        }

        generatedClip = AudioClip.Create(
            $"Unpleasant tone {safeFrequency:0} Hz",
            sampleCount,
            1,
            SampleRate,
            false);
        generatedClip.SetData(samples, 0);
        source.clip = generatedClip;
        source.Play();
    }

    public void StopPreview()
    {
        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }

        if (generatedClip != null)
        {
            Destroy(generatedClip);
            generatedClip = null;
        }
    }

    void OnDisable()
    {
        StopPreview();
    }

    void OnDestroy()
    {
        StopPreview();
    }

    void ConfigureSource()
    {
        if (source == null)
            source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0.65f;
        source.priority = 32;
        source.ignoreListenerPause = true;
    }
}
