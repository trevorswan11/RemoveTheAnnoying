using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "RefreshEnemiesList")]
[HarmonyPriority(Priority.Last)]
public class RemoveFogPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static bool Enabled => RemoveTheAnnoyingBase.Instance.RemoveInteriorFog.Value;

    private static void Postfix() {
        if (!Enabled) {
            Log.LogInfo("Remove fog disabled by user, I won't proceed.");
            return;
        }

        if (RoundManager.Instance == null) return;
        if (RoundManager.Instance.indoorFog == null) return;
        LocalVolumetricFog localFog = RoundManager.Instance.indoorFog;

        if (localFog == null) {
            Log.LogInfo("Fog was not detected in the current level.");
        } else {
            localFog.gameObject.SetActive(false);
            Log.LogInfo("Fog successfully disabled in current level.");
        }
    }
}
