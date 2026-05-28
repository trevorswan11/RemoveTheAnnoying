using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "RefreshEnemiesList")]
[HarmonyPriority(Priority.Last)]
public class RemoveFogPatch {
    private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
    private static readonly bool _enabled = RemoveTheAnnoyingBase.Instance.RemoveInteriorFog.Value;

    private static void Postfix() {
        if (!_enabled) {
            _log.LogInfo("Remove fog diabled by user, I won't proceed.");
            return;
        }

        if (RoundManager.Instance == null) return;
        if (RoundManager.Instance.indoorFog == null) return;
        LocalVolumetricFog localFog = RoundManager.Instance.indoorFog;


        if (localFog == null) { _log.LogInfo("Fog was not detected in the current level."); } else {
            localFog.gameObject.SetActive(false);
            _log.LogInfo("Fog successfully disabled in current level.");
        }
    }
}
