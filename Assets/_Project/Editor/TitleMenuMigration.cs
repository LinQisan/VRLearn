using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleMenuMigration
{
    const string ScenePath = "Assets/_Project/Scenes/TraficAcidentTitle.unity";
    const string MigratedOptionGuid = "b256aa60062d40bdb05f18b0217a5fd6";

    [InitializeOnLoadMethod]
    static void ScheduleAutomaticMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool("VRLearn.TitleMenuMigrated", false) ||
            File.ReadAllText(ScenePath).Contains(MigratedOptionGuid))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Run();
            SessionState.SetBool("VRLearn.TitleMenuMigrated", true);
        };
    }

    [MenuItem("Tools/VRLearn/Migrate Title Menu")]
    public static void Run()
    {
        var previousActiveScene = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var openedForMigration = !scene.IsValid() || !scene.isLoaded;
        if (openedForMigration)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        var director = FindInScene<GameDirector_Title>(scene);
        var centerEye = FindInScene<CenterEyeCamera>(scene);
        var canvas = FindTitleCanvas(scene);
        var canvasTransform = canvas.transform;

        var groups = new Dictionary<TitleOptionKind, ToggleGroup>();
        var options = new List<TitleOptionToggle>();
        var feedbackItems = new List<TitleButtonFeedback>();

        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var controlObject = button.gameObject;
            var oldOption = button.GetComponent<ButtonOption>();
            var tagName = button.tag;
            var objectName = button.name;

            if (TryGetOption(objectName, tagName, oldOption, out var kind, out var intValue, out var floatValue))
            {
                if (!groups.TryGetValue(kind, out var group))
                {
                    var groupHost = new GameObject($"{kind}ToggleGroup", typeof(RectTransform));
                    groupHost.transform.SetParent(canvas.transform, false);
                    group = groupHost.AddComponent<ToggleGroup>();
                    group.allowSwitchOff = false;
                    groups.Add(kind, group);
                }

                var colors = button.colors;
                var navigation = button.navigation;
                var targetGraphic = button.targetGraphic;
                var interactable = button.interactable;
                Object.DestroyImmediate(button);

                var toggle = controlObject.AddComponent<Toggle>();
                toggle.targetGraphic = targetGraphic;
                toggle.graphic = null;
                toggle.colors = colors;
                toggle.navigation = navigation;
                toggle.interactable = interactable;
                toggle.group = group;

                if (oldOption != null)
                    Object.DestroyImmediate(oldOption);

                var explicitOption = controlObject.AddComponent<TitleOptionToggle>();
                explicitOption.EditorConfigure(director, kind, intValue, floatValue);
                options.Add(explicitOption);
            }
            else if (TryGetCommand(tagName, out var command))
            {
                if (oldOption != null)
                    Object.DestroyImmediate(oldOption);
                var explicitCommand = controlObject.AddComponent<TitleCommandButton>();
                explicitCommand.EditorConfigure(director, command);
            }
            else if (oldOption != null)
            {
                // Obsolete inactive title controls must not retain Tag-driven listeners.
                Object.DestroyImmediate(oldOption);
            }

            var feedback = controlObject.GetComponent<TitleButtonFeedback>();
            if (feedback == null)
                feedback = controlObject.AddComponent<TitleButtonFeedback>();
            feedbackItems.Add(feedback);
        }

        // Include feedback attached to controls that were converted from Button to Toggle.
        foreach (var toggle in canvas.GetComponentsInChildren<Toggle>(true))
        {
            var feedback = toggle.GetComponent<TitleButtonFeedback>();
            if (feedback == null)
                feedback = toggle.gameObject.AddComponent<TitleButtonFeedback>();
            if (!feedbackItems.Contains(feedback))
                feedbackItems.Add(feedback);
        }

        director.EditorConfigure(
            canvas,
            FindFirstPath(canvasTransform,
                "90_PrimaryActions/Group_PrimaryActions", "ButtonGroupe_Main").gameObject,
            centerEye,
            FindText(canvasTransform, "10_EventParameters/Field_EventCount/EventNumber", "TextPanel_EventNumber/EventNumber"),
            FindText(canvasTransform, "10_EventParameters/Field_Height/Height", "TextPanel_Height/Height"),
            FindText(canvasTransform, "10_EventParameters/Field_Weight/Weight", "TextPanel_Weight/Weight"),
            FindText(canvasTransform, "10_EventParameters/Field_Age/Age", "TextPanel_Age/Age"),
            options.ToArray(),
            feedbackItems.ToArray());

        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Title menu migrated: {options.Count} toggles, {feedbackItems.Count} feedback controls.");

        if (openedForMigration)
            EditorSceneManager.CloseScene(scene, true);
        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
    }

    static bool TryGetOption(
        string objectName,
        string tagName,
        ButtonOption oldOption,
        out TitleOptionKind kind,
        out int intValue,
        out float floatValue)
    {
        kind = default;
        intValue = 0;
        floatValue = 0f;

        if (tagName == "Male") { kind = TitleOptionKind.Gender; intValue = 1; return true; }
        if (tagName == "Female") { kind = TitleOptionKind.Gender; intValue = 0; return true; }
        if (tagName == "GenderUnknown") { kind = TitleOptionKind.Gender; intValue = 2; return true; }
        if (tagName == "HaveLicense") { kind = TitleOptionKind.License; intValue = 1; return true; }
        if (tagName == "NoLicense") { kind = TitleOptionKind.License; intValue = 0; return true; }
        if (tagName == "LicenseUnknown") { kind = TitleOptionKind.License; intValue = 2; return true; }
        if (tagName == "YesIncident") { kind = TitleOptionKind.Incident; intValue = 1; return true; }
        if (tagName == "NoIncident") { kind = TitleOptionKind.Incident; intValue = 0; return true; }
        if (tagName == "IncidentUnknown") { kind = TitleOptionKind.Incident; intValue = 2; return true; }
        if (tagName == "DieFlashOn") { kind = TitleOptionKind.DieFlash; intValue = 1; return true; }
        if (objectName == "Button_DieFlashOff") { kind = TitleOptionKind.DieFlash; intValue = 0; return true; }
        if (tagName == "SmartPhoneOn") { kind = TitleOptionKind.SmartPhone; intValue = 1; return true; }
        if (objectName == "Button_SmartPhoneOff") { kind = TitleOptionKind.SmartPhone; intValue = 0; return true; }
        if (tagName == "WeatherClear") { kind = TitleOptionKind.Weather; intValue = 0; return true; }
        if (tagName == "WeatherRain") { kind = TitleOptionKind.Weather; intValue = 1; return true; }
        if (tagName == "TimeDay") { kind = TitleOptionKind.SkyTime; intValue = 0; return true; }
        if (tagName == "TimeNight") { kind = TitleOptionKind.SkyTime; intValue = 1; return true; }
        if (tagName == "HzTest" && oldOption != null)
        {
            kind = TitleOptionKind.Hz;
            floatValue = oldOption.HzTestValue;
            return true;
        }
        return false;
    }

    static bool TryGetCommand(string tagName, out TitleCommand command)
    {
        switch (tagName)
        {
            case "GameStart": command = TitleCommand.StartGame; return true;
            case "UpNumber": command = TitleCommand.EventUp; return true;
            case "DownNumber": command = TitleCommand.EventDown; return true;
            case "HeightUp": command = TitleCommand.HeightUp; return true;
            case "HeightDown": command = TitleCommand.HeightDown; return true;
            case "WeightUp": command = TitleCommand.WeightUp; return true;
            case "WeightDown": command = TitleCommand.WeightDown; return true;
            case "AgeUp": command = TitleCommand.AgeUp; return true;
            case "AgeDown": command = TitleCommand.AgeDown; return true;
            case "Reset": command = TitleCommand.ResetDefaults; return true;
            default: command = default; return false;
        }
    }

    static TMP_Text FindText(Transform root, params string[] paths)
    {
        return FindFirstPath(root, paths).GetComponent<TMP_Text>();
    }

    static Transform FindFirstPath(Transform root, params string[] paths)
    {
        foreach (var path in paths)
        {
            var target = root.Find(path);
            if (target != null)
                return target;
        }
        throw new System.InvalidOperationException($"Title UI path was not found: {string.Join(" or ", paths)}");
    }

    static Canvas FindTitleCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "Canvas_TitleMenu" || canvas.name == "Canvas_MainButton")
                    return canvas;
        throw new System.InvalidOperationException("Title Canvas was not found.");
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var result = root.GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }
        throw new System.InvalidOperationException($"{typeof(T).Name} was not found in {scene.path}");
    }

    static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var item in transforms)
                if (item.name == objectName)
                    return item.gameObject;
        }
        throw new System.InvalidOperationException($"Scene object was not found: {objectName}");
    }
}
