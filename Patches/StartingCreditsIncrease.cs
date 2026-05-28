using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(TimeOfDay), "Awake")]
public class StartingCreditsPatch
{
    private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
    private static readonly bool _enabled = RemoveTheAnnoyingBase.Instance.IncreasedStartingCredits.Value;
    private static readonly int _amount = CalculateDesired();

    private static void Postfix(TimeOfDay __instance)
    {
        if (!_enabled)
        {
            _log.LogInfo("Increased starting credits diabled by user, I won't proceed.");
            return;
        }

        __instance.quotaVariables.startingCredits = _amount;
        _log.LogInfo($"I set the starting credits to {_amount} successfully.");
    }

    private static int CalculateDesired()
    {
        int CruiserPrice = 400;
        int ArtificePrice = 1500;
        int WeedKillerPrice = 25;
        int FlashlightPrice = 25;
        int WalkiePrice = 12;
        int ShovelPrice = 30;

        return (
            CruiserPrice +
            ArtificePrice +
            2 * WeedKillerPrice +
            5 * FlashlightPrice +
            5 * WalkiePrice +
            2 * ShovelPrice
        );
    }
}
