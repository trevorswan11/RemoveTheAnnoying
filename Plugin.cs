using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RemoveTheAnnoying;

[BepInPlugin(GUID, NAME, VERSION)]
public class RemoveTheAnnoyingBase : BaseUnityPlugin {
    private const string GUID = "Kyoshi.RemoveTheAnnoying";
    private const string NAME = "RemoveTheAnnoying";
    private const string VERSION = "2.0.0";

    private readonly Harmony Harmony = new Harmony(GUID);
    public static RemoveTheAnnoyingBase Instance;
    public static ManualLogSource Log;

    public ConfigEntry<bool> MineshaftDisabled { get; private set; }
    public ConfigEntry<bool> BarberDisabled { get; private set; }
    public ConfigEntry<bool> ManeaterDisabled { get; private set; }
    public ConfigEntry<bool> AllowFactoryArtifice { get; private set; }
    public ConfigEntry<bool> CruiserTeleportFix { get; private set; }
    public ConfigEntry<bool> IncreasedArtificeScrap { get; private set; }
    public ConfigEntry<bool> AttemptForceManor { get; private set; }
    public ConfigEntry<bool> RemoveInteriorFog { get; private set; }
    public ConfigEntry<bool> IncreasedStartingCredits { get; private set; }
    public ConfigEntry<float> ShipLootDisplayTime { get; private set; }

    public void Awake() {
        // Singleton who
        if (Instance == null) Instance = this;

        Log = BepInEx.Logging.Logger.CreateLogSource(GUID);
        Log.LogInfo("Good heavens I'm patching it!");

        // Config
        BindConfig();
        Harmony.PatchAll();

        Log.LogInfo("I finished patching!");
        ConfigStatus();
    }

    private void BindConfig() {
        CruiserTeleportFix = Config.Bind("General", nameof(CruiserTeleportFix), true, "Allows players in a cruiser connected to the ship's magnet to be counted as in the ship when the ship takes off.");
        ShipLootDisplayTime = Config.Bind("General", nameof(ShipLootDisplayTime), 5.0f, "How long to display the total scrap value for in seconds. Set to 0 to disable.");

        MineshaftDisabled = Config.Bind("Interior Generation", nameof(MineshaftDisabled), true, "Disables mineshaft interior when enabled.");
        AllowFactoryArtifice = Config.Bind("Interior Generation", nameof(AllowFactoryArtifice), true, "Allows factory interior on Artifice when enabled.");
        AttemptForceManor = Config.Bind("Interior Generation", nameof(AttemptForceManor), false, "Attempts to force manor generation on all moons, when possible. Overrides all other interior config settings.");
        RemoveInteriorFog = Config.Bind("Interior Generation", nameof(RemoveInteriorFog), true, "Prevents the generation of interior fog introduced in v67.");

        BarberDisabled = Config.Bind("Enemies", nameof(BarberDisabled), true, "Disables all barber spawning when enabled.");
        ManeaterDisabled = Config.Bind("Enemies", nameof(ManeaterDisabled), true, "Disables all maneater spawning when enabled.");

        IncreasedArtificeScrap = Config.Bind("Money", nameof(IncreasedArtificeScrap), false, "Sets the minimum scrap of Artifice to 31 and the maximum to 37. These are the values from v56.");
        IncreasedStartingCredits = Config.Bind("Money", nameof(IncreasedStartingCredits), false, "Increases the starting credits enough to buy cruiser, 5 pro flashlights, 5 walkies, 2 shovels, 2 weed killer, and to go to Artifice (assuming no sales).");
    }

    private void ConfigStatus() {
        Log.LogDebug($"Config {nameof(MineshaftDisabled)} = {MineshaftDisabled.Value}");
        Log.LogDebug($"Config {nameof(BarberDisabled)} = {BarberDisabled.Value}");
        Log.LogDebug($"Config {nameof(ManeaterDisabled)} = {ManeaterDisabled.Value}");
        Log.LogDebug($"Config {nameof(AllowFactoryArtifice)} = {AllowFactoryArtifice.Value}");
        Log.LogDebug($"Config {nameof(CruiserTeleportFix)} = {CruiserTeleportFix.Value}");
        Log.LogDebug($"Config {nameof(IncreasedArtificeScrap)} = {IncreasedArtificeScrap.Value}");
        Log.LogDebug($"Config {nameof(AttemptForceManor)} = {AttemptForceManor.Value}");
        Log.LogDebug($"Config {nameof(RemoveInteriorFog)} = {RemoveInteriorFog.Value}");
        Log.LogDebug($"Config {nameof(IncreasedStartingCredits)} = {IncreasedStartingCredits.Value}");
        Log.LogDebug($"Config {nameof(ShipLootDisplayTime)} = {ShipLootDisplayTime.Value}");
    }
}
