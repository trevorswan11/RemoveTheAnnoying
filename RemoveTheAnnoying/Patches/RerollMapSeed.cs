using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System;

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
}
