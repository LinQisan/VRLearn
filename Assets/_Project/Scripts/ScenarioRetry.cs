using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reloads one scenario with the participant's current settings, before Start executes.</summary>
public static class ScenarioRetry
{
    static int[] settings;
    static float hz;
    static string destination;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        SceneManager.sceneLoaded -= Restore;
        settings = null;
        destination = null;
    }

    public static void Reload(GameDirector director)
    {
        if (director == null || settings != null)
            return;
        settings = new[] { director.EventNumber, director.Height, director.Weight, director.Gender,
            director.Age, director.License, director.SmartPhone, director.Incident, director.Weather,
            director.SkyTime, director.DieFlashNumber };
        hz = director.Hz;
        destination = SceneRoute.CurrentScene;
        Time.timeScale = 1f;
        OpenXRInput.StopControllerVibration();
        SceneManager.sceneLoaded += Restore;
        try { SceneManager.LoadScene(destination); }
        catch
        {
            Clear();
            throw;
        }
    }

    static void Restore(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != destination || settings == null)
            return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var director = root.GetComponentInChildren<GameDirector>(true);
            if (director == null)
                continue;
            director.EventNumber = settings[0]; director.Height = settings[1]; director.Weight = settings[2];
            director.Gender = settings[3]; director.Age = settings[4]; director.License = settings[5];
            director.SmartPhone = settings[6]; director.Incident = settings[7]; director.Weather = settings[8];
            director.SkyTime = settings[9]; director.DieFlashNumber = settings[10]; director.Hz = hz;
            break;
        }
        Clear();
    }
}
