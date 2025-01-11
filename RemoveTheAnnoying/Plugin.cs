﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using RemoveTheAnnoying.Patches;
using UnityEngine.Rendering.HighDefinition;

namespace RemoveTheAnnoying
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class RemoveAnnoyingBase : BaseUnityPlugin
    {
        private const string modGUID = "Kyoshi.RemoveAnnoyingStuff";
        private const string modName = "Remove Annoying Mechanics";
        private const string modVersion = "1.4.1";

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

        void Awake()
        {
            // Singleton who
            if (Instance == null) Instance = this;
            
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo("Patching some QoL files!");

            // Config
            BindConfig();
            ParseWeather(ForceWeather);
            PatchAll();            

            mls.LogInfo("The game is now more playable!");
            ConfigStatus();
        }

        void PatchAll()
        {
            // Base Patch
            harmony.PatchAll(typeof(RemoveAnnoyingBase));

            // All other patches
            harmony.PatchAll(typeof(CruiserSeatTeleportPatch));
            harmony.PatchAll(typeof(CruiserFailsafe));
            harmony.PatchAll(typeof(ChooseNewRandomMapSeedPatch));
            harmony.PatchAll(typeof(DisableBadEnemySpawningPatch));
            harmony.PatchAll(typeof(RemoveFogPatch));
            harmony.PatchAll(typeof(ArtificeScrapPatch));
            harmony.PatchAll(typeof(ForceWeatherPatch));
            harmony.PatchAll(typeof(StartingCreditsPatch));
            harmony.PatchAll(typeof(EclipsedScrapValuePatch));
        }

        void BindConfig()
        {
            CruiserFix = Config.Bind<bool>("General", "CruiserTeleportFix", true, "Allows players in a cruiser connected to the ship's magnet to be counted as in the ship when the ship takes off.");

            MineshaftDisabled = Config.Bind<bool>("Interior Generation", "DisableMineshaft", true, "Disables mineshaft interior when enabled.");
            AllowFactoryArtifice = Config.Bind<bool>("Interior Generation", "AllowArtificeFactory", true, "Allows factory interior on Artifice when enabled.");
            AttemptForceManor = Config.Bind<bool>("Interior Generation", "AttemptForceManor", false, "Attempts to force manor generation on all moons, when possible. Overrides all other interior config settings");
            RemoveInteriorFog = Config.Bind<bool>("Interior Generation", "RemoveInteriorFog", true, "Prevents the generation of interior fog introduced in v67.");

            BarberDisabled = Config.Bind<bool>("Enemies", "DisableBarber", true, "Disables all barber spawning when enabled.");
            ManeaterDisabled = Config.Bind<bool>("Enemies", "DisableManeater", true, "Disables all maneater spawning when enabled.");

            IncreasedArtificeScrap = Config.Bind<bool>("High Quota", "IncreasedArtificeScrap", false, "Sets the minimum scrap of Artifice to 31 and the maximum to 37. These are the values from v56.");

            ForceWeather = Config.Bind<string>("Training", "ForceWeather", "disabled", "Forces all moon's weathers to be the specified type, defaulting to 'disabled' if input is invalid. Case-insensitive choices are: None, Rainy, Stormy, Foggy, Flooded, Eclipsed, and disabled");

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
            mls.LogDebug($"Config IncreasedStartingCredits = {IncreasedStartingCredits.Value}");
            mls.LogDebug($"Config EclipsedMultiplier = {EclipsedMultiplier.Value}");
        }

        void ParseWeather(ConfigEntry<string> entry)
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

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(StartOfRound), "ChooseNewRandomMapSeed")]
    public class ChooseNewRandomMapSeedPatch
    {
        [HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
        public class GenerateNewFloorPatch
        {
            private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
            private static readonly bool MineshaftDisabled = RemoveAnnoyingBase.Instance.MineshaftDisabled.Value;
            private static readonly bool AllowFactoryArtifice = RemoveAnnoyingBase.Instance.AllowFactoryArtifice.Value;

            private static bool Prefix(RoundManager __instance)
            {
                string levelName = __instance.currentLevel.name.Replace("Level", "");
                try
                {
                    if (MineshaftDisabled)
                    {
                        // Modify the current level's dungeonFlowTypes by removing any entry where the id is the Mineshaft ID
                        __instance.currentLevel.dungeonFlowTypes = __instance.currentLevel.dungeonFlowTypes.Where(IsNotMineshaft).ToArray();
                        Logger.LogDebug($"Removed mineshaft generation of {levelName}.");
                    }

                    if (levelName.Equals("Artifice") && !AllowFactoryArtifice)
                    {
                        // Modify the current level's dungeonFlowTypes by removing any entry where the id is the Mineshaft ID
                        __instance.currentLevel.dungeonFlowTypes = __instance.currentLevel.dungeonFlowTypes.Where(IsNotFactory).ToArray();
                        Logger.LogDebug($"Removed factory generation of {levelName}.");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error removing interior type: {ex.Message}");
                    return false;
                }
            }

            private static bool IsNotMineshaft(IntWithRarity flow) => flow.id != (int)InteriorType.Mineshaft;
            private static bool IsNotFactory(IntWithRarity flow) => flow.id != (int)InteriorType.Factory;
        }

        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool MineshaftDisabled = RemoveAnnoyingBase.Instance.MineshaftDisabled.Value;
        private static readonly bool AllowFactoryArtifice = RemoveAnnoyingBase.Instance.AllowFactoryArtifice.Value;
        private static readonly bool ManorForced = RemoveAnnoyingBase.Instance.AttemptForceManor.Value;

        private const int MaxSeedAttempts = 1000;
        private const int MaxSeedValue = 100_000_000;

        private static readonly Dictionary<int?, string> interiorMap = new Dictionary<int?, string>();

        public enum InteriorType
        {
            Factory = 0, Manor = 1, Mineshaft = 4
        }

        private static void Postfix(StartOfRound __instance)
        {
            // Can exit early if the Mineshaft is enabled and Artifice is not banning factory
            if (!MineshaftDisabled && AllowFactoryArtifice)
            {
                Logger.LogInfo("All interiors are enabled, so I won't regenerate the seed.");
                return;
            }

            // Initializations
            Logger.LogDebug($"Initialize Dictionary: {InitializeInteriorDict()}");
            int randomSeed = __instance.randomMapSeed;
            RoundManager manager = RoundManager.Instance;
            InteriorType? type = DetermineType(randomSeed, manager);
            string levelName = __instance.currentLevel.name.Replace("Level", "");

            // Check if the interior type is valid
            if (!type.HasValue) return;

            type = type.Value;
            Logger.LogInfo($"Seed: {randomSeed} is a {type}.");
            InteriorType?[][] removeables = GetRemoveables();
            bool levelIsArtifice = levelName.Equals("Artifice");

            if (ManorForced)
            {
                if (!RemoveInteriorGeneration(type, removeables[5], manager, __instance))
                {
                    Logger.LogDebug("Forcing Manor was unsuccessful, defaulting to other interior config rules...");
                    if (MineshaftDisabled) RemoveInteriorGeneration(type, removeables[0], manager, __instance);
                    else if (!AllowFactoryArtifice && levelIsArtifice)
                    {
                        RemoveInteriorGeneration(type, removeables[2], manager, __instance);
                    }
                }
            }

            else if (MineshaftDisabled)
            {
                if (AllowFactoryArtifice)
                {
                    RemoveInteriorGeneration(type, removeables[0], manager, __instance);
                }
                else if (!AllowFactoryArtifice && levelIsArtifice)
                {
                    RemoveInteriorGeneration(type, removeables[5], manager, __instance);
                }
                else
                {
                    RemoveInteriorGeneration(type, removeables[0], manager, __instance);
                }
            }

            else if (!MineshaftDisabled)
            {
                // Only block Factory on artifice if requested
                if (!AllowFactoryArtifice && levelIsArtifice)
                {
                    RemoveInteriorGeneration(type, removeables[2], manager, __instance);
                }
            }
        }

        private static bool RemoveInteriorGeneration(InteriorType? currentType,
            InteriorType?[] disallowedTypes, RoundManager manager, StartOfRound __instance)
        {
            // Return if types are not provided, or if every interior is requested to be removed
            if (disallowedTypes.Length == 0 || disallowedTypes.Length == 3) return false;
            if (disallowedTypes == null || disallowedTypes.Contains(null)) return false;

            // Determine what the user wants to play
            if (!disallowedTypes.Contains(currentType))
            {
                Logger.LogInfo("No need to regenerate seed.");
                return false;
            }

            // Get the names of the disallowed types
            int?[] disallowed = disallowedTypes.Select(dt => (int?)dt.Value).ToArray();
            string[] names = disallowedTypes.Select(dt => interiorMap[(int)dt.Value]).ToArray();
            IEnumerable<string> zipped = names.Zip(disallowed, (name, typeVal) => $"{name}: {typeVal}");
            Logger.LogDebug($"Current: {currentType}; Disallowed: {string.Join(", ", zipped)}");

            // Log the types that are disallowed
            Logger.LogInfo($"{string.Join(" or ", names)} seed identified, trying to regenerate...");
            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            // Limit reroll attempts to the specified amount
            for (int i = 0; i < MaxSeedAttempts; i++)
            {
                int randomSeed = NewSeed();
                InteriorType? type = DetermineType((int)randomSeed, manager);
                Logger.LogDebug($"Attempt {i + 1} - Seed: {randomSeed} Interior: {type}");

                // Check for valid interior type
                if (!type.HasValue)
                {
                    Logger.LogWarning("Detected unknown interior.");
                    return false;
                }

                // Check for mineshaft or factory generation
                if (!disallowedTypes.Contains(new InteriorType?(type.Value).GetValueOrDefault()))
                {
                    __instance.randomMapSeed = randomSeed;
                    Logger.LogInfo($"Generated new map seed: {randomSeed} after {i + 1} attempts.");
                    return true;
                }
            }
            Logger.LogWarning("Regeneration failed after 1000 attempts");
            return false;
        }

        private static InteriorType? DetermineType(int seed, RoundManager manager)
        {
            try
            {
                // Realistically, this condiitonal will never be entered
                if (ManagerIsCompany(manager))
                {
                    Logger.LogDebug("The Company Building Detected.");
                    return null;
                }

                // This is 100000% necessary, do not remove this conditional
                if (manager.currentLevel.dungeonFlowTypes == null || manager.currentLevel.dungeonFlowTypes.Length == 0)
                {
                    Logger.LogDebug($"Seed {seed}: Moon is not recognized as having an interior.");
                    return null;
                }

                // 'seed' the random number so that it is the same sequence every time - this is what the game does as well
                System.Random rnd = new System.Random(seed);

                // Some debugging
                List<int> lst = manager.currentLevel.dungeonFlowTypes.Select((IntWithRarity flow) => flow.rarity).ToList();
                Logger.LogDebug("List: " + string.Join(", ", lst));
                int weight = manager.GetRandomWeightedIndex(lst.ToArray(), rnd);
                Logger.LogDebug($"Weight: {weight}");

                // Check the enum for the id
                int id = manager.currentLevel.dungeonFlowTypes[weight].id;
                if (Enum.IsDefined(typeof(InteriorType), id))
                {
                    return (InteriorType)id;
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error determining interior type for seed {seed}: {ex.Message}");
                return null;
            }
        }

        private static int NewSeed() => new System.Random().Next(1, MaxSeedValue);

        private static bool ManagerIsCompany(RoundManager manager)
        {
            string levelName = manager.currentLevel.name.Replace("Level", "");
            return levelName.Equals("CompanyBuilding");
        }

        private static bool InitializeInteriorDict()
        {
            if (interiorMap.ContainsKey(0)) return false;
            interiorMap.Add(0, "Factory");
            interiorMap.Add(1, "Manor");
            interiorMap.Add(4, "Mineshaft");
            return true;
        }

        /// <summary>
        /// Indices: 0 is Mine, 1 is Manor, 2 is Fact, 3 = Mine/Manor, 4 is Fact/Manor, 5 is Mine/Fact
        /// </summary>
        private static InteriorType?[][] GetRemoveables()
        {
            InteriorType?[][] toRemove = new InteriorType?[6][];
            toRemove[0] = new InteriorType?[] { InteriorType.Mineshaft };
            toRemove[1] = new InteriorType?[] { InteriorType.Manor };
            toRemove[2] = new InteriorType?[] { InteriorType.Factory };
            toRemove[3] = new InteriorType?[] { InteriorType.Mineshaft, InteriorType.Manor };
            toRemove[4] = new InteriorType?[] { InteriorType.Factory, InteriorType.Manor };
            toRemove[5] = new InteriorType?[] { InteriorType.Mineshaft, InteriorType.Factory };
            return toRemove;
        }
    }

    [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
    public class DisableBadEnemySpawningPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly HashSet<string> DisabledEnemies
            = new HashSet<string> { "ClaySurgeon", "CaveDweller" };
        private static readonly bool BarberDisabled = RemoveAnnoyingBase.Instance.BarberDisabled.Value;
        private static readonly bool ManeaterDisabled = RemoveAnnoyingBase.Instance.ManeaterDisabled.Value;

        private static void Prefix(SelectableLevel newLevel)
        {
            try { LevelOperation(newLevel, false); }
            catch (Exception ex)
            {
                Logger.LogWarning($"Prefix disabling ran incorrectly: {ex}");
            }
        }

        private static void Postfix(SelectableLevel newLevel)
        {
            try { LevelOperation(newLevel, true); }
            catch (Exception ex)
            {
                Logger.LogWarning($"Postfix disabling ran incorrectly: {ex}");
            }
        }

        private static void LevelOperation(SelectableLevel newLevel, bool log)
        {
            // Check for company
            if (SelectableLevelIsCompany(newLevel)) return;

            // Check if the user is ok with both enemies
            if (!BarberDisabled && !ManeaterDisabled)
            {
                if (log) Logger.LogInfo("All unfun enemies allowed by user config.");
                return;
            }

            // Check if the level contains any of the disabled enemies
            if (!newLevel.Enemies.Any(e => DisabledEnemies.Contains(e.enemyType.name)))
            {
                if (log) Logger.LogInfo("No unfun enemies detected in spawning pool.");
                return;
            }

            // Check and count disabled enemies, modify along the way
            int disabledCount = 0;
            foreach (SpawnableEnemyWithRarity e in newLevel.Enemies)
            {
                if (DisableEnemyIfStinky(e, log)) disabledCount++;
            }
            if (log) Logger.LogInfo($"Disabled {disabledCount} unfun enemies in current level.");
            if (log && disabledCount > 0) Logger.LogDebug("Level will not spawn any unfun enemies.");
        }

        private static bool DisableEnemyIfStinky(SpawnableEnemyWithRarity enemy, bool log)
        {
            string enemyName = enemy.enemyType.name;
            if (DisabledEnemies.Contains(enemyName))
            {
                // Check to see if the user is ok with the barber
                if (enemyName.Equals("ClaySurgeon") && !BarberDisabled)
                {
                    if (log) Logger.LogInfo("Barber allowed due to user config.");
                    return false;
                }

                // Check to see if the user is ok with the maneater
                if (enemyName.Equals("CaveDweller") && !ManeaterDisabled)
                {
                    if (log) Logger.LogInfo("Maneater allowed due to user config");
                    return false;
                }

                enemy.rarity = 0;
                enemy.enemyType.spawningDisabled = true;
                if (log) Logger.LogInfo($"Spawning of {enemyName} disabled.");
                return true;
            }
            return false;
        }

        private static bool SelectableLevelIsCompany(SelectableLevel selectableLevel)
        {
            string levelName = selectableLevel.name.Replace("Level", "");
            return levelName.Equals("CompanyBuilding");
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
    public class CruiserSeatTeleportPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool CruiserFixEnabled = RemoveAnnoyingBase.Instance.CruiserFix.Value;
        private static readonly float TeleportDelay = 4.642f;

        private async static void Postfix(StartOfRound __instance)
        {
            // Check to see if the ship is leaving or Magnet is not on
            if (!IsShipLeaving(__instance)) return;

            // Check the current config option
            if (!CruiserFixEnabled)
            {
                Logger.LogInfo("Cruiser fix diabled by user, I won't proceed.");
                return;
            }
            await ExecuteAfterDelay(TeleportDelay, __instance);
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
            Logger.LogDebug("Teleport sequence exited without any errors :)");
        }

        private static bool IsShipLeaving(StartOfRound startOfRound)
        {
            return startOfRound.shipIsLeaving || startOfRound.shipLeftAutomatically;
        }

        private static bool IsMagnetProper(StartOfRound startOfRound, VehicleController vehicleController)
        {
            bool result = startOfRound.magnetOn && (startOfRound.isObjectAttachedToMagnet || vehicleController.magnetedToShip);
            if (!result) Logger.LogInfo("Teleport sequence exited due to cruiser not being connected to the ship's magnet.");
            return result;
        }

        private static bool TeleportPlayerToTerminal(PlayerControllerB player)
        {
            if (player == null) return false;
            Terminal term = UnityEngine.Object.FindObjectOfType<Terminal>();
            player.TeleportPlayer(term.transform.position);
            Logger.LogInfo($"Successfully teleported {player.playerUsername} to Ship.");
            player.isInHangarShipRoom = true;
            return true;
        }
    }

    [HarmonyPatch(typeof(ElevatorAnimationEvents), "ElevatorFullyRunning")]
    public class CruiserFailsafe
    {
        private static readonly bool CruiserFixEnabled = RemoveAnnoyingBase.Instance.CruiserFix.Value;

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

    [HarmonyPatch(typeof(StartOfRound), "SetPlanetsWeather")]
    public class ForceWeatherPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly string ForceWeather = RemoveAnnoyingBase.Instance.ForceWeather.Value;

        private static bool Prefix(SelectableLevel[] ___levels)
        {
            if (ForceWeather == null || ForceWeather.Equals("disabled"))
            {
                Logger.LogInfo("ForceWeather input was not identified.");
                return true;
            }
            LevelWeatherType type;
            switch (ForceWeather.ToLower())
            {
                case "none":
                    type = LevelWeatherType.None; break;
                case "rainy":
                    type = LevelWeatherType.Rainy; break;
                case "stormy":
                    type = LevelWeatherType.Stormy; break;
                case "foggy":
                    type = LevelWeatherType.Foggy; break;
                case "flooded":
                    type = LevelWeatherType.Flooded; break;
                case "eclipsed":
                    type = LevelWeatherType.Eclipsed; break;
                default:
                    Logger.LogInfo("ForceWeather input was not identified.");
                    return true;
            }

            foreach (SelectableLevel level in ___levels)
            {
                Logger.LogInfo($"Setting {level.name.Replace("Level", "")}'s weather to {ForceWeather}.");
                level.currentWeather = type;
            }
            Logger.LogInfo($"All level's weather set to {ForceWeather}.");
            return false;
        }
    }

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

    [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
    public class EclipsedScrapValuePatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly float Multiplier = RemoveAnnoyingBase.Instance.EclipsedMultiplier.Value;

        public static void Prefix(RoundManager __instance)
        {
            if (Multiplier <= 0 ||  Multiplier > 2 || Multiplier == 1)
            {
                Logger.LogInfo($"Given multiplier was {Multiplier}, I won't proceed.");
                return;
            }

            if ((int)TimeOfDay.Instance.currentLevelWeather == 5) typeof(RoundManager).GetField("scrapValueMultiplier").SetValue(__instance, Multiplier);
            Logger.LogInfo($"I set the spawned scrap multiplier to {Multiplier} successfully.");
        }
    }
}
