using System.Threading.Tasks;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches.Core
{
    [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
    public class CruiserSeatTeleportPatch
    {
        private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
        private static readonly bool _enabled = RemoveTheAnnoyingBase.Instance.CruiserTeleportFix.Value;
        private static readonly float _teleportDelay = 4.642f;

        private async static void Postfix(StartOfRound __instance)
        {
            // Check to see if the ship is leaving or Magnet is not on
            if (!IsShipLeaving(__instance)) return;

            // Check the current config option
            if (!_enabled)
            {
                _log.LogInfo("Cruiser fix diabled by user, I won't proceed.");
                return;
            }
            await ExecuteAfterDelay(_teleportDelay, __instance);
        }

        private static async Task ExecuteAfterDelay(float delay, StartOfRound __instance)
        {
            await Task.Delay((int)(delay * 1000));
            TeleportSequence(__instance);
        }

        private static void TeleportSequence(StartOfRound __instance)
        {
            VehicleController cruiser = __instance.attachedVehicle;
            if (!IsMagnetProper(__instance, cruiser)) return;
            PlayerControllerB playerA = cruiser.currentDriver;
            TeleportPlayerToTerminal(playerA);
            PlayerControllerB playerB = cruiser.currentPassenger;
            TeleportPlayerToTerminal(playerB);
            _log.LogDebug("Teleport sequence exited without any errors :)");
        }

        private static bool IsShipLeaving(StartOfRound startOfRound)
        {
            return startOfRound.shipIsLeaving || startOfRound.shipLeftAutomatically;
        }

        private static bool IsMagnetProper(StartOfRound startOfRound, VehicleController vehicleController)
        {
            bool result = startOfRound.magnetOn && (startOfRound.isObjectAttachedToMagnet || vehicleController.magnetedToShip);
            if (!result) _log.LogInfo("Teleport sequence exited due to cruiser not being connected to the ship's magnet.");
            return result;
        }

        private static bool TeleportPlayerToTerminal(PlayerControllerB player)
        {
            if (player == null) return false;
            Terminal term = UnityEngine.Object.FindObjectOfType<Terminal>();
            player.TeleportPlayer(term.transform.position);
            _log.LogInfo($"Successfully teleported {player.playerUsername} to Ship.");
            player.isInHangarShipRoom = true;
            return true;
        }
    }

    [HarmonyPatch(typeof(ElevatorAnimationEvents), "ElevatorFullyRunning")]
    public class CruiserFailsafePatch
    {
        private static readonly bool CruiserFixEnabled = RemoveTheAnnoyingBase.Instance.CruiserTeleportFix.Value;

        private static void Prefix()
        {
            if (!CruiserFixEnabled) return;
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            for (int i = 0; i < 100; i++)
            {
                if (player.physicsParent == null) continue;
                VehicleController vehicle = player.physicsParent.GetComponentInParent<VehicleController>();
                if (vehicle && vehicle.magnetedToShip) player.isInElevator = true;
            }
        }
    }
}
