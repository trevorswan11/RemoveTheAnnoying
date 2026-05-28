using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(TimeOfDay), "Awake")]
public class StartingCreditsPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static bool Enabled => RemoveTheAnnoyingBase.Instance.IncreasedStartingCredits.Value;

    private const int JUICED_AMT = CRUISER_PRICE +
        1 * ART_PRICE +
        2 * WEED_PRICE +
        5 * PRO_PRICE +
        5 * WALKIE_PRICE +
        2 * SHOVEL_PRICE;

    private static void Postfix(TimeOfDay __instance) {
        if (!Enabled) {
            Log.LogInfo("Increased starting credits diabled by user, I won't proceed.");
            return;
        }

        __instance.quotaVariables.startingCredits = JUICED_AMT;
        Log.LogInfo($"I set the starting credits to {JUICED_AMT} successfully.");
    }

    private const int CRUISER_PRICE = 370;
    private const int ART_PRICE = 1500;
    private const int WEED_PRICE = 20;
    private const int PRO_PRICE = 28;
    private const int WALKIE_PRICE = 10;
    private const int SHOVEL_PRICE = 30;
}
