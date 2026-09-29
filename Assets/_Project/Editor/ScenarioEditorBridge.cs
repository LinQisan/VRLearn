using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lets the web scenario editor (ScenarioEditor/) start a scenario in this Editor: its
/// "Unityで試す" button writes Scenarios/.play-request.json; this watcher picks it up, deletes it
/// and plays the file in the Hikone scene. Ignored while playing or compiling.
/// </summary>
[InitializeOnLoad]
public static class ScenarioEditorBridge
{
    [Serializable] sealed class PlayRequest { public string path; }

    public static string RequestPath => Path.Combine(CustomScenarioLibrary.ProjectFolder, ".play-request.json");

    static double nextCheck;

    static ScenarioEditorBridge()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck)
            return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(RequestPath))
            return;

        string path;
        try
        {
            path = JsonUtility.FromJson<PlayRequest>(File.ReadAllText(RequestPath))?.path;
            File.Delete(RequestPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ScenarioEditor] unreadable play request: " + exception.Message);
            TryDelete();
            return;
        }
        var folder = CustomScenarioLibrary.ProjectFolder;
        var full = string.IsNullOrEmpty(path) ? "" : Path.GetFullPath(Path.Combine(folder, path));
        if (!full.StartsWith(folder, StringComparison.Ordinal) || !File.Exists(full))
        {
            Debug.LogWarning("[ScenarioEditor] play request for a file outside Scenarios/ or missing: " + path);
            return;
        }
        Debug.Log("[ScenarioEditor] playing " + full);
        CustomScenarioTools.Play(full);
    }

    static void TryDelete()
    {
        try { File.Delete(RequestPath); } catch { /* next poll retries */ }
    }
}
