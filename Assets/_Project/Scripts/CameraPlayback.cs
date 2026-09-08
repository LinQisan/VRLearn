using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraPlayback : MonoBehaviour
{
    public RawImage displayImage;    // 再生用のUI（RawImage）
    public float frameRate = 30;     // 再生フレームレート

    private List<Texture2D> frames;
    private bool isPlaying = false;

    public void PlayRecording(List<Texture2D> recordedFrames)
    {
        if (recordedFrames == null || recordedFrames.Count == 0 || isPlaying)
            return;
        frames = recordedFrames;
        StartCoroutine(PlayFrames());
    }

    private IEnumerator PlayFrames()
    {
        isPlaying = true;

        foreach (var frame in frames)
        {
            displayImage.texture = frame;
            yield return new WaitForSeconds(1f / frameRate);
        }

        isPlaying = false;
    }
}
