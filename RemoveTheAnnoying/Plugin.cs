using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RemoveTheAnnoying.Patches;
using UnityEngine;

namespace RemoveTheAnnoying
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class RemoveAnnoyingBase : BaseUnityPlugin
    {
        private const string modGUID = "Kyoshi.RemoveAnnoyingStuff";
        private const string modName = "Remove Annoying Mechanics";
        private const string modVersion = "1.0.1";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static RemoveAnnoyingBase Instance;

        public static ManualLogSource mls;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo("Patching some QoL files!");

            harmony.PatchAll(typeof(RemoveAnnoyingBase));
            harmony.PatchAll(typeof(ChooseNewRandomMapSeedPatch));
            harmony.PatchAll(typeof(DisableBadEnemySpawningPatch));

            mls.LogInfo("The game is now more playable!");
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

        private static void Postfix(StartOfRound __instance)
        {
            // Get the seed and the level's manager and determine its type
            int randomSeed = __instance.randomMapSeed;
            RoundManager manager = RoundManager.Instance;
            InteriorType? type = DetermineType(randomSeed, manager);

           if (manager.currentLevel.name.Equals("Gordion") || 
                manager.currentLevel.PlanetName.Equals("Gordion") ||
                __instance.currentLevel.PlanetName.Equals("Gordion") ||
                __instance.currentLevel.name.Equals("Gordion"))
            {
                Logger.LogInfo("The Company Building Detected.");
                return;
            }

            // Check if the interior type is valid
            if (!type.HasValue)
            {
                return;
            }
            type = type.Value;
            Logger.LogInfo($"Seed: {randomSeed} is a {type}.");

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
            for (int i = 0; i < 1000; i++)
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

        [HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
        public class GenerateNewFloorPatch
        {
            private static bool Prefix(RoundManager __instance)
            {
                __instance.currentLevel.dungeonFlowTypes = __instance.currentLevel.dungeonFlowTypes.Where((IntWithRarity dungeonFlowType) => dungeonFlowType.id != 4).ToArray();
                Logger.LogInfo((object)("Removed mineshaft generation of " + ((UnityEngine.Object)__instance.currentLevel) + "."));
                return true;
            }
        }

        /// <summary>
        /// Uses a given seed and round manager to determine the interior type.
        /// </summary>
        /// <param name="seed">The current map seed as an int.</param>
        /// <param name="manager">The custom current RoundManager object.</param>
        /// <returns>The type of the map given the seed, or null if not found.</returns>
        private static InteriorType? DetermineType(int seed, RoundManager manager)
        {
            if (manager.currentLevel.name.Equals("Gordion") ||
                manager.currentLevel.PlanetName.Equals("Gordion"))
            {
                Logger.LogInfo("The Company Building Detected.");
                return null;
            }

            if (manager.currentLevel.dungeonFlowTypes == null || manager.currentLevel.dungeonFlowTypes.Length == 0)
            {
                return null;
            }

            System.Random rnd = new System.Random(seed);
            List<int> lst = manager.currentLevel.dungeonFlowTypes.Select((IntWithRarity flow) => flow.rarity).ToList();
            Logger.LogDebug("List: " + string.Join(", ", lst));
            int weight = manager.GetRandomWeightedIndex(lst.ToArray(), rnd);
            Logger.LogDebug($"Weight: {weight}");
            int id = manager.currentLevel.dungeonFlowTypes[weight].id;

            // Check the enum for the id
            if (Enum.IsDefined(typeof(InteriorType), id))
            { 
                return (InteriorType)id;
            }
            return null;
        }

        /// <summary>
        /// Generates a new random seed.
        /// </summary>
        /// <returns>An int value between 1 and 100 million</returns>
        private static int NewSeed() => new System.Random().Next(1, 100_000_000);
    }

    [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
    public class DisableBadEnemySpawningPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;

        private static void Prefix(SelectableLevel newLevel)
        {
            foreach (SpawnableEnemyWithRarity e in newLevel.Enemies)
            {
                // Check for the barber
                if (e.enemyType.name.Equals("ClaySurgeon"))
                {
                    DisableEnemy(e);
                }
                
                // Check for the maneater
                if (e.enemyType.name.Equals("CaveDweller"))
                {
                    DisableEnemy(e);
                }
            }
        }

        private static void DisableEnemy(SpawnableEnemyWithRarity enemy)
        {
            enemy.rarity = 0;
            Logger.LogInfo($"Spawning of {enemy.enemyType.name} disabled.");
        }
    }
}
