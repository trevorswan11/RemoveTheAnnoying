using BepInEx.Logging;
using HarmonyLib;
using RemoveTheAnnoying.Util;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
public class ArtificeScrapPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static bool Enabled => RemoveTheAnnoyingBase.Instance.IncreasedArtificeScrap.Value;

    private const int V56_ART_MIN = 31;
    private const int V56_ART_MAX = 37;

    private static void Prefix(SelectableLevel ___currentLevel) {
        // Check the config option set by user
        if (!Enabled) {
            Log.LogInfo("Artifice scrap increase disabled, I won't proceed.");
            return;
        }

        if (Level.Is(___currentLevel, "Artifice")) {
            Log.LogDebug("Altering Artifice scrap spawn-rates...");
            ___currentLevel.minScrap = V56_ART_MIN;
            ___currentLevel.maxScrap = V56_ART_MAX;
            Log.LogInfo($"I successfully updated Artifice's scrap to a range of ({V56_ART_MIN},{V56_ART_MAX})");
            return;
        }
        Log.LogInfo("Current moon is not Artifice.");
        return;
    }
}
