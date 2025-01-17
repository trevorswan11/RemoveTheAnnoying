using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(TimeOfDay), "Awake")]
    public class StartingCreditsPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool IncreaseEnabled = RemoveAnnoyingBase.Instance.IncreasedStartingCredits.Value;
        private static readonly int IncreasedAmount = CalculateDesired();

        private static void Postfix(TimeOfDay __instance)
        {
            if (!IncreaseEnabled)
            {
                Logger.LogInfo("Increased starting credits diabled by user, I won't proceed.");
                return;
            }

            __instance.quotaVariables.startingCredits = IncreasedAmount;
            Logger.LogInfo($"I set the starting credits to {IncreasedAmount} successfully.");
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
}
