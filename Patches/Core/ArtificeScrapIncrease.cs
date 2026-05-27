using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches.Core
{
    [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
    public class ArtificeScrapPatch
    {
        private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
        private static readonly bool _enabled = RemoveTheAnnoyingBase.Instance.IncreasedArtificeScrap.Value;

        private const int V56_ART_MIN = 31;
        private const int V56_ART_MAX = 37;

        private static void Prefix(SelectableLevel ___currentLevel)
        {
            // Check the config option set by user
            if (!_enabled)
            {
                _log.LogInfo("Artifice scrap increase disabled, I won't proceed.");
                return;
            }

            // Check if the player is actually on art
            string levelName = ___currentLevel.name.Replace("Level", "");
            if (levelName.Equals("Artifice"))
            {
                _log.LogDebug("Attempting to alter scrap spawn-rates...");
                ___currentLevel.minScrap = V56_ART_MIN;
                ___currentLevel.maxScrap = V56_ART_MAX;
                _log.LogInfo($"I successfully updated Artifice's scrap to a range of ({V56_ART_MIN},{V56_ART_MAX})");
                return;
            }
            _log.LogInfo("Current moon is not Artifice.");
            return;
        }
    }
}
