using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
    public class ArtificeScrapPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool IncreasedArtificeScrapEnabled = RemoveAnnoyingBase.Instance.IncreasedArtificeScrap.Value;

        public static int v56ArtMin = 31;
        public static int v56ArtMax = 37;

        private static void Prefix(SelectableLevel ___currentLevel)
        {
            // Check the config option set by user
            if (!IncreasedArtificeScrapEnabled)
            {
                Logger.LogInfo("Artifice scrap increase diabled, I won't proceed.");
                return;
            }

            // Check if the player is actually on art
            string levelName = ___currentLevel.name.Replace("Level", "");
            if (levelName.Equals("Artifice"))
            {
                Logger.LogDebug("Attempting to alter scrap spawnrates...");
                ___currentLevel.minScrap = v56ArtMin;
                ___currentLevel.maxScrap = v56ArtMax;
                Logger.LogInfo($"I successfully updated Artifice's scrap to a range of ({v56ArtMin},{v56ArtMax})");
                return;
            }
            Logger.LogInfo("Current moon is not Artifice.");
            return;
        }
    }
}
