using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Centralizes routing for the supported Meta title and gameplay scenes.</summary>
public static class SceneRoute
{
    public const string MetaTitle = "TraficAcidentTitle_Meta";
    public const string MetaGameplay = "TraficAcident_Meta";
    public const string HikoneGameplay = "TraficAcident_Hikone_Meta";

    /// <summary>Gameplay environments selectable on the title screen (index = title option value).</summary>
    public const int StandardEnvironment = 0;
    public const int HikoneEnvironment = 1;
    static readonly string[] GameplayScenes = { MetaGameplay, HikoneGameplay };

    /// <summary>
    /// The standard environment is currently blocked: the title offers no choice and every
    /// start/restart routes to Hikone. The scene stays in the build (and in the regression
    /// tests), so flipping this flag restores it.
    /// </summary>
    public const bool StandardEnvironmentSelectable = false;
    public const int DefaultEnvironment = HikoneEnvironment;

    /// <summary>
    /// Environment used by Start/Reset. Set by the title screen and kept in sync with the
    /// gameplay scene actually loaded, so returning to the title shows the environment in use.
    /// </summary>
    public static int SelectedEnvironment { get; set; }

    public static int ClampEnvironment(int value)
    {
        value = Mathf.Clamp(value, 0, GameplayScenes.Length - 1);
        return !StandardEnvironmentSelectable && value == StandardEnvironment ? DefaultEnvironment : value;
    }

    public static string GameplayScene(int environment) => GameplayScenes[ClampEnvironment(environment)];

    /// <summary>Environment index of a gameplay scene name, or -1 for non-gameplay scenes.</summary>
    public static int EnvironmentOfScene(string sceneName) => System.Array.IndexOf(GameplayScenes, sceneName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEnvironmentSelection()
    {
        SelectedEnvironment = DefaultEnvironment;
        SceneManager.activeSceneChanged -= TrackGameplayEnvironment;
        SceneManager.activeSceneChanged += TrackGameplayEnvironment;
    }

    static void TrackGameplayEnvironment(Scene previous, Scene next)
    {
        var environment = EnvironmentOfScene(next.name);
        if (environment >= 0)
            SelectedEnvironment = environment;
    }

    /// <summary>Valid accident scenario ID range. Must match ScenarioRuntime (exactly 10 entries, 0-9).</summary>
    public const int MinScenarioId = 0;
    public const int MaxScenarioId = 9;

    /// <summary>Title-side sentinel meaning "pick a random scenario on StartGame/Reset/BackTitle".</summary>
    public const int RandomScenarioId = -1;

    /// <summary>Single source of truth for random scenario draws. UnityEngine.Random max is exclusive.</summary>
    public static int DrawRandomScenarioId() => Random.Range(MinScenarioId, MaxScenarioId + 1);

    public static bool IsValidScenarioId(int id) => id >= MinScenarioId && id <= MaxScenarioId;

    /// <summary>
    /// Clamps a stored selection to a legal value. RANDOM is preserved as-is;
    /// explicit IDs are clamped to Min..Max. Never throws for out-of-range input.
    /// </summary>
    public static int ClampScenarioSelection(int value) =>
        CustomScenarioSession.IsCustom(value) && CustomScenarioSession.Current != null
            ? value      // a JSON scenario chosen on the title's custom tab
            : Mathf.Clamp(value, RandomScenarioId, MaxScenarioId);

    /// <summary>
    /// Steps the title selection (RANDOM + Min..Max, 11 choices) with wraparound.
    /// Out-of-range input self-heals into the legal cycle instead of propagating.
    /// </summary>
    public static int StepScenarioSelection(int current, int delta)
    {
        var choiceCount = (MaxScenarioId - MinScenarioId + 1) + 1;
        var choiceIndex = current - RandomScenarioId;
        choiceIndex = ((choiceIndex + delta) % choiceCount + choiceCount) % choiceCount;
        return choiceIndex + RandomScenarioId;
    }

    public static bool IsMetaScene(string sceneName) => sceneName.EndsWith("_Meta");

    public static string GameplayForCurrentScene =>
        GameplayScene(SelectedEnvironment);

    public static string TitleForCurrentScene =>
        MetaTitle;

    public static string CurrentScene => SceneManager.GetActiveScene().name;
}
