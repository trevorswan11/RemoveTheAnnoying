using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(RoundManager), "RefreshEnemiesList")]
    [HarmonyPriority(Priority.Last)]
    public class RemoveFogPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool RemoveFogEnabled = RemoveAnnoyingBase.Instance.RemoveInteriorFog.Value;

        private static void Postfix()
        {
            if (!RemoveFogEnabled)
            {
                Logger.LogInfo("Remove fog diabled by user, I won't proceed.");
                return;
            }

            if (RoundManager.Instance == null) return;
            if (RoundManager.Instance.indoorFog == null) return;
            LocalVolumetricFog localFog = RoundManager.Instance.indoorFog;
            bool result = DisableFog(localFog);

            if (result) Logger.LogInfo("Fog successfully disabled in current level.");
            else Logger.LogInfo("Fog was not detected in the current level or disabling was unsuccessful.");
        }

        private static bool DisableFog(LocalVolumetricFog localFog)
        {
            if (localFog == null) return false;
            localFog.gameObject.SetActive(false);
            return true;
        }
    }
}
