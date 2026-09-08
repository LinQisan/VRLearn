using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRecorder : MonoBehaviour
{
    public Camera targetCamera;         // 録画対象のカメラ
    public int frameRate = 30;          // 録画フレームレート
    private List<Texture2D> frames;     // フレームを保存するリスト
    private bool isRecording = false;   // 録画中フラグ

    void Start()
    {
        frames = new List<Texture2D>();
    }

    public void StartRecording()
    {
        isRecording = true;
        StartCoroutine(RecordFrames());
    }

    public void StopRecording()
    {
        isRecording = false;
    }

    private IEnumerator RecordFrames()
    {
        while (isRecording)
        {
            yield return new WaitForSeconds(1f / frameRate);
            CaptureFrame();
        }
    }

    private void CaptureFrame()
    {
        RenderTexture renderTexture = targetCamera.targetTexture;

        // RenderTexture から Texture2D を作成
        Texture2D frame = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        frame.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        frame.Apply();
        RenderTexture.active = null;

        frames.Add(frame);
    }

    public List<Texture2D> GetRecordedFrames()
    {
        return frames;
    }
}