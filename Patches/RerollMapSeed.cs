using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using RemoveTheAnnoying.Util;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
public class GenerateNewFloorPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static bool MineshaftDisabled => RemoveTheAnnoyingBase.Instance.MineshaftDisabled.Value;
    private static bool AllowArtFactory => RemoveTheAnnoyingBase.Instance.AllowFactoryArtifice.Value;

    private static bool Prefix(RoundManager __instance) {
        try {
            if (MineshaftDisabled) {
                __instance.currentLevel.dungeonFlowTypes =
                                    [.. __instance.currentLevel.dungeonFlowTypes.Where(f => f.id != (int)InteriorType.Mineshaft)];
                Log.LogDebug($"Removed Mineshaft generation for {__instance.currentLevel.name}.");
            }

            if (Level.Is(__instance.currentLevel, "Artifice") && !AllowArtFactory) {
                __instance.currentLevel.dungeonFlowTypes =
                                    [.. __instance.currentLevel.dungeonFlowTypes.Where(f => f.id != (int)InteriorType.Factory)];
                Log.LogDebug($"Removed Factory generation for {__instance.currentLevel.name}.");
            }
            return true;
        } catch (Exception ex) {
            Log.LogWarning($"Error removing interior type: {ex.Message}");
            return false;
        }
    }
}

[HarmonyPatch(typeof(StartOfRound), "ChooseNewRandomMapSeed")]
public class ChooseNewRandomMapSeedPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static bool MineshaftDisabled => RemoveTheAnnoyingBase.Instance.MineshaftDisabled.Value;
    private static bool AllowArtFactory => RemoveTheAnnoyingBase.Instance.AllowFactoryArtifice.Value;
    private static bool ManorForced => RemoveTheAnnoyingBase.Instance.AttemptForceManor.Value;

    private const int MAX_SEED_ATTEMPTS = 1000;
    private const int MAX_SEED_VALUE = 100_000_000;
    private static readonly Random Rng = new();

    private static readonly IReadOnlyDictionary<InteriorType, string> InteriorNames =
            new Dictionary<InteriorType, string> {
                [InteriorType.Factory] = "Factory",
                [InteriorType.Manor] = "Manor",
                [InteriorType.Mineshaft] = "Mineshaft",
            };

    // Decides which interior types should not be generated
    internal static InteriorType[] BuildDisallowList(
            bool isArtifice,
            bool mineshaftDisabled,
            bool allowArtFactory,
            bool manorForced) {
        if (manorForced) {
            return [InteriorType.Factory, InteriorType.Mineshaft];
        }

        bool blockFactory = !allowArtFactory && isArtifice;
        return (mineshaftDisabled, blockFactory) switch {
            (true, true) => [InteriorType.Mineshaft, InteriorType.Factory],
            (true, false) => [InteriorType.Mineshaft],
            (false, true) => [InteriorType.Factory],
            (false, false) => [],
        };
    }

    private static void Postfix(StartOfRound __instance) {
        // Can exit early if the Mineshaft is enabled and Artifice is not banning factory
        InteriorType[] disallowed = BuildDisallowList(
                    isArtifice: Level.Is(__instance.currentLevel, "Artifice"),
                    mineshaftDisabled: MineshaftDisabled,
                    allowArtFactory: AllowArtFactory,
                    manorForced: ManorForced);

        if (disallowed.Length == 0) {
            Log.LogInfo("All interiors are enabled, so I won't regenerate the seed.");
            return;
        }

        RoundManager manager = RoundManager.Instance;
        TryRegenerateSeed(disallowed, manager, __instance);
    }

    private static void TryRegenerateSeed(
            InteriorType[] disallowed,
            RoundManager manager,
            StartOfRound round) {
        int currentSeed = round.randomMapSeed;
        InteriorType? currentType = DetermineType(currentSeed, manager);
        if (currentType is null) return;

        Log.LogInfo($"Seed {currentSeed} = {currentType}.");
        if (!disallowed.Contains(currentType.Value)) {
            Log.LogInfo("No need to regenerate seed as interior is acceptable.");
            return;
        }

        string disallowedNames = string.Join(" or ", disallowed.Select(t => InteriorNames[t]));
        Log.LogInfo($"{disallowedNames} seed identified, trying to regenerate...");

        // Prepare the manager for seed regeneration
        manager.hasInitializedLevelRandomSeed = false;
        manager.InitializeRandomNumberGenerators();
        for (int i = 0; i < MAX_SEED_ATTEMPTS; i++) {
            int candidateSeed = NextSeed();
            InteriorType? candidateType = DetermineType(candidateSeed, manager);
            Log.LogDebug($"Reroll Attempt {i + 1} - Seed: {candidateSeed}, Interior: {candidateType}");

            if (candidateType is null) {
                Log.LogWarning("Detected unknown interior.");
                return;
            }

            if (!disallowed.Contains(candidateType.Value)) {
                round.randomMapSeed = candidateSeed;
                Log.LogInfo($"Generated new map seed: {candidateSeed} after {i + 1} reroll attempts.");
                return;
            }
        }
        Log.LogWarning($"Regeneration failed after {MAX_SEED_ATTEMPTS} attempts");
    }

    private static InteriorType? DetermineType(int seed, RoundManager manager) {
        try {
            // Realistically, this conditional will never be entered
            if (Level.IsCompanyBuilding(manager.currentLevel)) {
                return null;
            }

            // This is 100000% necessary, do not remove this conditional
            var flows = manager.currentLevel.dungeonFlowTypes;
            if (flows is null || flows.Length == 0) {
                Log.LogDebug($"Seed {seed}: moon has no interior flow types.");
                return null;
            }

            // The game's RNG must be mirrored to predict seed outcome
            var rnd = new Random(seed);
            int[] weights = [.. flows.Select(f => f.rarity)];
            Log.LogDebug($"Rarities: [{string.Join(", ", weights)}]");

            int weightedIdx = manager.GetRandomWeightedIndex(weights, rnd);
            int id = flows[weightedIdx].id;
            Log.LogDebug($"Weighted Index {weightedIdx} => ID {id}");
            return Enum.IsDefined(typeof(InteriorType), id) ? (InteriorType)id : null;
        } catch (Exception ex) {
            Log.LogWarning($"Error determining interior type for seed {seed}: {ex.Message}");
            return null;
        }
    }

    private static int NextSeed() => Rng.Next(1, MAX_SEED_VALUE);
}

public enum InteriorType {
    Factory = 0, Manor = 1, Mineshaft = 4
}
