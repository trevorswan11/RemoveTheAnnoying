using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RemoveTheAnnoying.Patches;

namespace RemoveTheAnnoying
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class RemoveAnnoyingBase : BaseUnityPlugin
    {
        private const string modGUID = "Kyoshi.RemoveAnnoyingStuff";
        private const string modName = "Remove Annoying Mechanics";
        private const string modVersion = "1.4.2";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static RemoveAnnoyingBase Instance;
        public static ManualLogSource mls;

        public ConfigEntry<bool> MineshaftDisabled { get; private set; }
        public ConfigEntry<bool> BarberDisabled { get; private set; }
        public ConfigEntry<bool> ManeaterDisabled { get; private set; }
        public ConfigEntry<bool> AllowFactoryArtifice { get; private set; }
        public ConfigEntry<bool> CruiserFix { get; private set; }
        public ConfigEntry<bool> IncreasedArtificeScrap { get; private set; }
        public ConfigEntry<bool> AttemptForceManor { get; private set; }
        public ConfigEntry<bool> RemoveInteriorFog { get; private set; }
        public ConfigEntry<string> ForceWeather { get; private set; }
        public ConfigEntry<float> EclipsedMultiplier { get; private set; }
        public ConfigEntry<bool> IncreasedStartingCredits { get; private set; }
        public ConfigEntry<string> DisableWeather { get; private set; }

        void Awake()
        {
            // Singleton who
            if (Instance == null) Instance = this;
            
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo("Patching some QoL files!");

            // Config
            BindConfig();
            ParseWeather(ForceWeather, DisableWeather);
            Patch(
                typeof(CruiserSeatTeleportPatch),
                typeof(CruiserFailsafePatch),
                typeof(ChooseNewRandomMapSeedPatch),
                typeof(DisableBadEnemySpawningPatch),
                typeof(RemoveFogPatch),
                typeof(ArtificeScrapPatch),
                typeof(WeatherPatch),
                typeof(StartingCreditsPatch),
                typeof(EclipsedScrapValuePatch)
            );

            mls.LogInfo("The game is now more playable!");
            ConfigStatus();
        }

        void BindConfig()
        {
            CruiserFix = Config.Bind<bool>("General", "CruiserTeleportFix", true, "Allows players in a cruiser connected to the ship's magnet to be counted as in the ship when the ship takes off.");

            MineshaftDisabled = Config.Bind<bool>("Interior Generation", "DisableMineshaft", true, "Disables mineshaft interior when enabled.");
            AllowFactoryArtifice = Config.Bind<bool>("Interior Generation", "AllowArtificeFactory", true, "Allows factory interior on Artifice when enabled.");
            AttemptForceManor = Config.Bind<bool>("Interior Generation", "AttemptForceManor", false, "Attempts to force manor generation on all moons, when possible. Overrides all other interior config settings.");
            RemoveInteriorFog = Config.Bind<bool>("Interior Generation", "RemoveInteriorFog", true, "Prevents the generation of interior fog introduced in v67.");

            BarberDisabled = Config.Bind<bool>("Enemies", "DisableBarber", true, "Disables all barber spawning when enabled.");
            ManeaterDisabled = Config.Bind<bool>("Enemies", "DisableManeater", true, "Disables all maneater spawning when enabled.");

            IncreasedArtificeScrap = Config.Bind<bool>("High Quota", "IncreasedArtificeScrap", false, "Sets the minimum scrap of Artifice to 31 and the maximum to 37. These are the values from v56.");

            ForceWeather = Config.Bind<string>("Training", "ForceWeather", "disabled", "Forces all moon's weathers to be the specified type, defaulting to 'disabled' if input is invalid. Case-insensitive choices are: None, Rainy, Stormy, Foggy, Flooded, Eclipsed, and disabled");
            DisableWeather = Config.Bind<string>("Training", "DisableWeather", "disabled", "Disables the specified weather from occuring on all moons. Is disabled by default or if input cannot be parsed. Will be disabled if ForceWeather has a vlaue that is not 'disabled'. Case-insensitive choices are: None, Rainy, Stormy, Foggy, Flooded, Eclipsed, and disabled.");

            IncreasedStartingCredits = Config.Bind<bool>("Relaxed", "IncreasedStartingCredits", false, "Increases the starting credits enough to buy cruiser, 5 pro flashlights, 5 walkies, 2 shovels, 2 weed killer, and to go to Artifice (assuming no sales).");
            EclipsedMultiplier = Config.Bind<float>("Relaxed", "EclipsedMultiplier", 1.0f, "Alters the global scrap value multiplier for eclipsed moons. The valid input range is (0-2]. A little goes a long way...");
        }

        void ConfigStatus()
        {
            mls.LogDebug($"Config CruiserTeleportFix = {CruiserFix.Value}");
            mls.LogDebug($"Config DisableMineshaft = {MineshaftDisabled.Value}");
            mls.LogDebug($"Config AllowArtificeFactory = {AllowFactoryArtifice.Value}");
            mls.LogDebug($"Config AttemptForceManor = {AttemptForceManor.Value}");
            mls.LogDebug($"Config RemoveInteriorFog = {RemoveInteriorFog.Value}");
            mls.LogDebug($"Config DisableBarber = {BarberDisabled.Value}");
            mls.LogDebug($"Config DisableManeater = {ManeaterDisabled.Value}");
            mls.LogDebug($"Config IncreasedArtificeScrap = {IncreasedArtificeScrap.Value}");
            mls.LogDebug($"Config ForceWeather = {ForceWeather.Value}");
            mls.LogDebug($"Config DisableWeather = {DisableWeather.Value}");
            mls.LogDebug($"Config IncreasedStartingCredits = {IncreasedStartingCredits.Value}");
            mls.LogDebug($"Config EclipsedMultiplier = {EclipsedMultiplier.Value}");
        }

        void Patch(params System.Type[] patchNames)
        {
            harmony.PatchAll(typeof(RemoveAnnoyingBase));
            foreach (System.Type patchName in patchNames) harmony.PatchAll(patchName);
        }

        void ParseWeather(params ConfigEntry<string>[] entries)
        {
            foreach (ConfigEntry<string> entry in entries)
            {
                string value = entry.Value.ToLower();
                switch (value)
                {
                    case "disabled":
                    case "none":
                    case "rainy":
                    case "stormy":
                    case "foggy":
                    case "flooded":
                    case "eclipsed":
                        entry.Value = value;
                        break;
                    default:
                        mls.LogDebug("Could not parse forced weather.");
                        entry.Value = "disabled";
                        break;
                }
            }
        }
    }
}
