using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(StartOfRound), "ChooseNewRandomMapSeed")]
public class ChooseNewRandomMapSeedPatch
{
    private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
    private static readonly bool _mineshaftDisabled = RemoveTheAnnoyingBase.Instance.MineshaftDisabled.Value;
    private static readonly bool _allowArtFactory = RemoveTheAnnoyingBase.Instance.AllowFactoryArtifice.Value;
    private static readonly bool _manorForced = RemoveTheAnnoyingBase.Instance.AttemptForceManor.Value;

    private const int MAX_SEED_ATTEMPTS = 1000;
    private const int MAX_SEED_VALUE = 100_000_000;

    private static readonly Dictionary<int?, string> _interiorMap = new Dictionary<int?, string>{
            {0, "Factory" },
            {1, "Manor"},
            {4, "Mineshaft"}
        };

    public enum InteriorType
    {
        Factory = 0, Manor = 1, Mineshaft = 4
    }

    /// Indices: 0 is Mine, 1 is Manor, 2 is Fact, 3 = Mine/Manor, 4 is Fact/Manor, 5 is Mine/Fact
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

    [HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
    public class GenerateNewFloorPatch
    {
        private static bool Prefix(RoundManager __instance)
        {
            string levelName = __instance.currentLevel.name.Replace("Level", "");
            try
            {
                if (_mineshaftDisabled)
                {
                    // Modify the current level's dungeonFlowTypes by removing any entry where the id is the Mineshaft ID
                    __instance.currentLevel.dungeonFlowTypes = __instance.currentLevel.dungeonFlowTypes.Where(IsNotMineshaft).ToArray();
                    _log.LogDebug($"Removed mineshaft generation of {levelName}.");
                }

                if (levelName.Equals("Artifice") && !_allowArtFactory)
                {
                    // Modify the current level's dungeonFlowTypes by removing any entry where the id is the Mineshaft ID
                    __instance.currentLevel.dungeonFlowTypes = __instance.currentLevel.dungeonFlowTypes.Where(IsNotFactory).ToArray();
                    _log.LogDebug($"Removed factory generation of {levelName}.");
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning($"Error removing interior type: {ex.Message}");
                return false;
            }
        }

        private static bool IsNotMineshaft(IntWithRarity flow) => flow.id != (int)InteriorType.Mineshaft;
        private static bool IsNotFactory(IntWithRarity flow) => flow.id != (int)InteriorType.Factory;
    }

    private static void Postfix(StartOfRound __instance)
    {
        // Can exit early if the Mineshaft is enabled and Artifice is not banning factory
        if (!_mineshaftDisabled && _allowArtFactory)
        {
            _log.LogInfo("All interiors are enabled, so I won't regenerate the seed.");
            return;
        }

        // Initializations
        int randomSeed = __instance.randomMapSeed;
        RoundManager manager = RoundManager.Instance;
        InteriorType? type = DetermineType(randomSeed, manager);
        string levelName = __instance.currentLevel.name.Replace("Level", "");

        // Check if the interior type is valid
        if (!type.HasValue) return;

        type = type.Value;
        _log.LogInfo($"Seed: {randomSeed} is a {type}.");
        InteriorType?[][] removeables = GetRemoveables();
        bool levelIsArtifice = levelName.Equals("Artifice");

        if (_manorForced)
        {
            if (!RemoveInteriorGeneration(type, removeables[5], manager, __instance))
            {
                _log.LogDebug("Forcing Manor was unsuccessful, defaulting to other interior config rules...");
                if (_mineshaftDisabled) RemoveInteriorGeneration(type, removeables[0], manager, __instance);
                else if (!_allowArtFactory && levelIsArtifice)
                {
                    RemoveInteriorGeneration(type, removeables[2], manager, __instance);
                }
            }
        }

        else if (_mineshaftDisabled)
        {
            if (_allowArtFactory)
            {
                RemoveInteriorGeneration(type, removeables[0], manager, __instance);
            }
            else if (!_allowArtFactory && levelIsArtifice)
            {
                RemoveInteriorGeneration(type, removeables[5], manager, __instance);
            }
            else
            {
                RemoveInteriorGeneration(type, removeables[0], manager, __instance);
            }
        }

        else if (!_mineshaftDisabled)
        {
            // Only block Factory on artifice if requested
            if (!_allowArtFactory && levelIsArtifice)
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
            _log.LogInfo("No need to regenerate seed.");
            return false;
        }

        // Get the names of the disallowed types
        int?[] disallowed = disallowedTypes.Select(dt => (int?)dt.Value).ToArray();
        string[] names = disallowedTypes.Select(dt => _interiorMap[(int)dt.Value]).ToArray();
        IEnumerable<string> zipped = names.Zip(disallowed, (name, typeVal) => $"{name}: {typeVal}");
        _log.LogDebug($"Current: {currentType}; Disallowed: {string.Join(", ", zipped)}");

        // Log the types that are disallowed
        _log.LogInfo($"{string.Join(" or ", names)} seed identified, trying to regenerate...");
        manager.hasInitializedLevelRandomSeed = false;
        manager.InitializeRandomNumberGenerators();

        for (int i = 0; i < MAX_SEED_ATTEMPTS; i++)
        {
            int randomSeed = NewSeed();
            InteriorType? type = DetermineType(randomSeed, manager);
            _log.LogDebug($"Reroll Attempt {i + 1} - Seed: {randomSeed} Interior: {type}");

            // Check for valid interior type
            if (!type.HasValue)
            {
                _log.LogWarning("Detected unknown interior.");
                return false;
            }

            // Check for mineshaft or factory generation
            if (!disallowedTypes.Contains(new InteriorType?(type.Value).GetValueOrDefault()))
            {
                __instance.randomMapSeed = randomSeed;
                _log.LogInfo($"Generated new map seed: {randomSeed} after {i + 1} reroll attempts.");
                return true;
            }
        }
        _log.LogWarning("Regeneration failed after 1000 attempts");
        return false;
    }

    private static InteriorType? DetermineType(int seed, RoundManager manager)
    {
        try
        {
            // Realistically, this condiitonal will never be entered
            if (ManagerIsCompany(manager))
            {
                _log.LogDebug("The Company Building Detected.");
                return null;
            }

            // This is 100000% necessary, do not remove this conditional
            if (manager.currentLevel.dungeonFlowTypes == null || manager.currentLevel.dungeonFlowTypes.Length == 0)
            {
                _log.LogDebug($"Seed {seed}: Moon is not recognized as having an interior.");
                return null;
            }

            // 'seed' the random number so that it is the same sequence every time - this is what the game does as well
            System.Random rnd = new System.Random(seed);

            // Some debugging
            List<int> lst = manager.currentLevel.dungeonFlowTypes.Select((IntWithRarity flow) => flow.rarity).ToList();
            _log.LogDebug("List: " + string.Join(", ", lst));
            int weight = manager.GetRandomWeightedIndex(lst.ToArray(), rnd);
            _log.LogDebug($"Weight: {weight}");

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
            _log.LogWarning($"Error determining interior type for seed {seed}: {ex.Message}");
            return null;
        }
    }

    private static int NewSeed() => new Random().Next(1, MAX_SEED_VALUE);

    private static bool ManagerIsCompany(RoundManager manager)
    {
        string levelName = manager.currentLevel.name.Replace("Level", "");
        return levelName.Equals("CompanyBuilding");
    }
}
