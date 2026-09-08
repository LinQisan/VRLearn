using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Centralizes routing for the supported Meta title and gameplay scenes.</summary>
public static class SceneRoute
{
    public const string MetaTitle = "TraficAcidentTitle_Meta";
    public const string MetaGameplay = "TraficAcident_Meta";

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
        Mathf.Clamp(value, RandomScenarioId, MaxScenarioId);

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
        MetaGameplay;

    public static string TitleForCurrentScene =>
        MetaTitle;

    public static string CurrentScene => SceneManager.GetActiveScene().name;
}
