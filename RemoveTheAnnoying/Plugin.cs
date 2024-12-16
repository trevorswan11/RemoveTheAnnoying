using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using RemoveTheAnnoying.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemoveTheAnnoying
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class RemoveAnnoyingBase : BaseUnityPlugin
    {
        private const string modGUID = "Kyoshi.RemoveAnnoyingStuff";
        private const string modName = "Remove Annoying Mechanics";
        private const string modVersion = "1.1.1";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static RemoveAnnoyingBase Instance;
        public static ManualLogSource mls;

        public ConfigEntry<bool> MineshaftDisabled { get; private set; }
        public ConfigEntry<bool> BarberDisabled { get; private set; }
        public ConfigEntry<bool> ManeaterDisabled { get; private set; }
        public ConfigEntry<bool> AllowFactoryArtifice { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            MineshaftDisabled = Config.Bind<bool>("General", "DisableMineshaft", true, "Disables mineshaft interior when enabled.");
            BarberDisabled = Config.Bind<bool>("General", "DisableBarber", true, "Disables all barber spawning when enabled.");
            ManeaterDisabled = Config.Bind<bool>("General", "DisableManeater", true, "Disables all maneater spawning when enabled.");
            AllowFactoryArtifice = Config.Bind<bool>("General", "AllowArtificeFactory", true, "Allows factory interior on Artifice when enabled.");

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo("Patching some QoL files!");

            harmony.PatchAll(typeof(RemoveAnnoyingBase));
            harmony.PatchAll(typeof(ChooseNewRandomMapSeedPatch));
            harmony.PatchAll(typeof(DisableBadEnemySpawningPatch));

            mls.LogInfo("The game is now more playable!");
            ConfigStatus();
        }

        void ConfigStatus()
        {
            mls.LogDebug($"Config DisableMineshaft = {MineshaftDisabled.Value}");
            mls.LogDebug($"Config DisableBarber = {BarberDisabled.Value}");
            mls.LogDebug($"Config DisableManeater = {ManeaterDisabled.Value}");
            mls.LogDebug($"Config AllowArtificeFactory = {AllowFactoryArtifice.Value}");
        }
    }

    /// <summary>
    /// The different interior types for the current version.
    /// </summary>
    public enum InteriorType
    {
        Factory = 0,
        Manor = 1,
        Mineshaft = 4
    }
}

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(StartOfRound), "ChooseNewRandomMapSeed")]
    public class ChooseNewRandomMapSeedPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly bool MineshaftDisabled = RemoveAnnoyingBase.Instance.MineshaftDisabled.Value;
        private static readonly bool AllowFactoryArtifice = RemoveAnnoyingBase.Instance.AllowFactoryArtifice.Value;

        private const int MaxSeedAttempts = 1000;
        private const int MaxSeedValue = 100_000_000;

        // Postfix method - runs after round seed is generated
        private static void Postfix(StartOfRound __instance)
        {
            // Can exit early if the Mineshaft is enabled and Artifice is not banning factory
            if (!MineshaftDisabled && AllowFactoryArtifice)
            {
                Logger.LogInfo("All interiors are enabled, so I won't regenerate the seed.");
                return;
            }

            // Get the seed and the level's manager and determine its type
            int randomSeed = __instance.randomMapSeed;
            RoundManager manager = RoundManager.Instance;
            InteriorType? type = DetermineType(randomSeed, manager);
            string levelName = __instance.currentLevel.name.Replace("Level", "");
            
            // Check for The Company - I don't know if any of this is necessary
            if (ManagerIsCompany(manager) || StartOfRoundIsCompany(__instance))
            {
                Logger.LogDebug("The Company Building Detected.");
                return;
            }

            // Check if the interior type is valid
            if (!type.HasValue) return;

            type = type.Value;
            Logger.LogInfo($"Seed: {randomSeed} is a {type}.");

            // Mineshaft disabled and artifice CAN spawn factory
            if (MineshaftDisabled && AllowFactoryArtifice)
            {
                RemoveMineshaft(type, manager, randomSeed, __instance);
            }

            // Mineshaft disabled and artifice CANNOT spawn factory
            else if (MineshaftDisabled && !AllowFactoryArtifice)
            {
                // Only force manor on artifice
                if (levelName.Equals("Artifice")) ManorOnly(type, manager, randomSeed, __instance);
                else RemoveMineshaft(type, manager, randomSeed, __instance);
            }

            // Mineshaft enabled and artifice cannot spawn factory
            else if (!MineshaftDisabled && !AllowFactoryArtifice)
            {
                // Only block Factory on artifice
                if (levelName.Equals("Artifice")) RemoveFactory(type, manager, randomSeed, __instance);
            }
        }

        // Removes mineshaft generation and regenerates to find a new seed
        private static void RemoveMineshaft(InteriorType? type, RoundManager manager, int randomSeed, StartOfRound __instance)
        {
            // Check if the map is ok
            if (type.GetValueOrDefault() != InteriorType.Mineshaft)
            {
                Logger.LogInfo("No need to regenerate seed.");
                return;
            }

            // Otherwise the map must be a mineshaft
            Logger.LogInfo("Mineshaft seed identified, trying to regenerate...");
            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            // Limit to 1000 total generation attempts
            for (int i = 0; i < MaxSeedAttempts; i++)
            {
                randomSeed = NewSeed();
                type = DetermineType((int)randomSeed, manager);
                Logger.LogDebug($"Attempt {i + 1} - Seed: {randomSeed} Interior: {type}");

                // Check for valid interior type
                if (!type.HasValue)
                {
                    Logger.LogWarning("Detected unknown interior.");
                    return;
                }

                // Check for mineshaft
                if (new InteriorType?(type.Value).GetValueOrDefault() != InteriorType.Mineshaft)
                {
                    __instance.randomMapSeed = randomSeed;
                    Logger.LogInfo($"Generated new map seed: {randomSeed} after {i + 1} attempts.");
                    return;
                }
            }
            Logger.LogWarning("Regeneration failed after 1000 attempts");
        }

        // Removes mineshaft generation and regenerates to find a new seed
        private static void RemoveFactory(InteriorType? type, RoundManager manager, int randomSeed, StartOfRound __instance)
        {
            // Check if the map is ok
            if (type.GetValueOrDefault() != InteriorType.Factory)
            {
                Logger.LogInfo("No need to regenerate seed.");
                return;
            }

            // Otherwise the map must be a factory
            Logger.LogInfo("Factory seed identified, trying to regenerate...");
            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            // Limit to 1000 total generation attempts
            for (int i = 0; i < MaxSeedAttempts; i++)
            {
                randomSeed = NewSeed();
                type = DetermineType((int)randomSeed, manager);
                Logger.LogDebug($"Attempt {i + 1} - Seed: {randomSeed} Interior: {type}");

                // Check for valid interior type
                if (!type.HasValue)
                {
                    Logger.LogWarning("Detected unknown interior.");
                    return;
                }

                // Check for mineshaft
                if (new InteriorType?(type.Value).GetValueOrDefault() != InteriorType.Factory)
                {
                    __instance.randomMapSeed = randomSeed;
                    Logger.LogInfo($"Generated new map seed: {randomSeed} after {i + 1} attempts.");
                    return;
                }
            }
            Logger.LogWarning("Regeneration failed after 1000 attempts");
        }

        // Removes both Factory and Mineshaft interior
        private static void ManorOnly(InteriorType? type, RoundManager manager, int randomSeed, StartOfRound __instance)
        {
            // Check if the map is ok
            if (type.GetValueOrDefault() != InteriorType.Mineshaft &&
                type.GetValueOrDefault() != InteriorType.Factory)
            {
                Logger.LogInfo("No need to regenerate seed.");
                return;
            }

            // Otherwise the map must be a mineshaft or factory
            Logger.LogInfo("Mineshaft or Factory seed identified, trying to regenerate...");
            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            // Limit to 1000 total generation attempts
            for (int i = 0; i < MaxSeedAttempts; i++)
            {
                randomSeed = NewSeed();
                type = DetermineType((int)randomSeed, manager);
                Logger.LogDebug($"Attempt {i + 1} - Seed: {randomSeed} Interior: {type}");

                // Check for valid interior type
                if (!type.HasValue)
                {
                    Logger.LogWarning("Detected unknown interior.");
                    return;
                }

                // Check for mineshaft or factory generation
                if (new InteriorType?(type.Value).GetValueOrDefault() != InteriorType.Mineshaft &&
                    new InteriorType?(type.Value).GetValueOrDefault() != InteriorType.Factory)
                {
                    __instance.randomMapSeed = randomSeed;
                    Logger.LogInfo($"Generated new map seed: {randomSeed} after {i + 1} attempts.");
                    return;
                }
            }
            Logger.LogWarning("Regeneration failed after 1000 attempts");
        }

        /// <summary>
        /// Uses a given seed and round manager to determine the interior type.
        /// </summary>
        /// <param name="seed">The current map seed as an int.</param>
        /// <param name="manager">The custom current RoundManager object.</param>
        /// <returns>The type of the map given the seed, or null if not found.</returns>
        private static InteriorType? DetermineType(int seed, RoundManager manager)
        {
            try
            {
                // My dumbass throwing shit at the wall and hoping it sticks
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

        /// <summary>
        /// Generates a new random seed.
        /// </summary>
        /// <returns>An int value between 1 and 100 million</returns>
        private static int NewSeed() => new System.Random().Next(1, MaxSeedValue);

        private static bool ManagerIsCompany(RoundManager manager)
        {
            return manager.currentLevel.name.Equals("Gordion") ||
                manager.currentLevel.PlanetName.Equals("Gordion") ||
                manager.currentLevel.Equals("CompanyBuilding");
        }

        private static bool StartOfRoundIsCompany(StartOfRound startOfRound)
        {
            return startOfRound.currentLevel.PlanetName.Equals("Gordion") ||
                startOfRound.currentLevel.name.Equals("Gordion") ||
                startOfRound.currentLevel.sceneName.Equals("CompanyBuilding");
        }

        [HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
        public class GenerateNewFloorPatch
        {
            // Prefix method - Runs before GenerateNewFloor does in the RoundManager class
            private static bool Prefix(RoundManager __instance)
            {
                string levelName = __instance.currentLevel.name.Replace("Level", "");

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

            private static bool IsNotMineshaft(IntWithRarity flow) => flow.id != (int)InteriorType.Mineshaft;
            private static bool IsNotFactory(IntWithRarity flow) => flow.id != (int)InteriorType.Factory;
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

        // Prefix method - runs before the RoundManager loads the new level
        private static void Prefix(SelectableLevel newLevel)
        {
            // Check if the user is ok with both enemies
            if (!BarberDisabled && !ManeaterDisabled)
            {
                Logger.LogInfo("All unfun enemies allowed by user config.");
                return;
            }

            // Check if the level contains any of the disabled enemies
            if (!newLevel.Enemies.Any(e => DisabledEnemies.Contains(e.enemyType.name)))
            {
                Logger.LogInfo("No unfun enemies detected in spawning pool.");
                return;
            }

            // Check and count disabled enemies, modify along the way
            int disabledCount = 0;
            foreach (SpawnableEnemyWithRarity e in newLevel.Enemies)
            {
                if (DisableEnemyIfStinky(e)) disabledCount++;
            }
            Logger.LogInfo($"Disabled {disabledCount} unfun enemies in current level.");
            Logger.LogDebug("Level will not spawn any unfun enemies.");
        }

        /// <summary>
        /// Given any enemy, sets its rarity (spawn chance) to 0 if in set of disabled enemies
        /// </summary>
        /// <param name="enemy">The desired enemy to alter.</param>
        private static bool DisableEnemyIfStinky(SpawnableEnemyWithRarity enemy)
        {
            string enemyName = enemy.enemyType.name;
            if (DisabledEnemies.Contains(enemyName))
            {
                // Check to see if the user is ok with the barber
                if (enemyName.Equals("ClaySurgeon") && !BarberDisabled)
                {
                    Logger.LogInfo("Barber allowed due to user config.");
                    return false;
                }

                // Check to see if the user is ok with the maneater
                if (enemyName.Equals("CaveDweller") && !ManeaterDisabled)
                {
                    Logger.LogInfo("Maneater allowed due to user config");
                    return false;
                }

                enemy.rarity = 0;
                Logger.LogInfo($"Spawning of {enemyName} disabled.");
                return true;
            }
            return false;
        }
    }
}
