using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System;
using RemoveTheAnnoying.Util;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
public class DisableBadEnemySpawningPatch {
    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static HashSet<string> DisabledEnemies => ["ClaySurgeon", "CaveDweller"];
    private static bool NoBarber => RemoveTheAnnoyingBase.Instance.BarberDisabled.Value;
    private static bool NoManeater => RemoveTheAnnoyingBase.Instance.ManeaterDisabled.Value;

    private static void Prefix(SelectableLevel newLevel) {
        try { DisableEnemies(newLevel, false); } catch (Exception ex) {
            Log.LogWarning($"Prefix disabling ran incorrectly: {ex}");
        }
    }

    private static void Postfix(SelectableLevel newLevel) {
        try {
            DisableEnemies(newLevel, true);
        } catch (Exception ex) {
            Log.LogWarning($"Postfix disabling ran incorrectly: {ex}");
        }
    }

    private static void DisableEnemies(SelectableLevel newLevel, bool log) {
        // Check for company
        if (Level.IsCompanyBuilding(newLevel)) return;

        // Check if the user is ok with both enemies
        if (!NoBarber && !NoManeater) {
            if (log) Log.LogInfo("All unfun enemies allowed by user config.");
            return;
        }

        // Check if the level contains any of the disabled enemies
        if (!newLevel.Enemies.Any(e => DisabledEnemies.Contains(e.enemyType.name))) {
            if (log) Log.LogInfo("No unfun enemies detected in spawning pool.");
            return;
        }

        // Check and count disabled enemies, modify along the way
        int disabledCount = 0;
        foreach (SpawnableEnemyWithRarity e in newLevel.Enemies) {
            if (DisableEnemyIfStinky(e, log)) disabledCount++;
        }

        if (log) {
            Log.LogInfo($"Disabled {disabledCount} unfun enemies in current level.");
            if (disabledCount > 0) Log.LogDebug("Level will not spawn any unfun enemies.");
        }
    }

    private static bool DisableEnemyIfStinky(SpawnableEnemyWithRarity enemy, bool log) {
        string enemyName = enemy.enemyType.name;
        if (DisabledEnemies.Contains(enemyName)) {
            if (enemyName.Equals("ClaySurgeon") && !NoBarber) return false;
            if (enemyName.Equals("CaveDweller") && !NoManeater) return false;

            enemy.rarity = 0;
            enemy.enemyType.spawningDisabled = true;
            if (log) Log.LogInfo($"Spawning of {enemyName} disabled.");
            return true;
        }
        return false;
    }
}
