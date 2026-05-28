using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RemoveTheAnnoying.Patches;

[HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
public class DisableBadEnemySpawningPatch {
    private static readonly ManualLogSource _log = RemoveTheAnnoyingBase.Log;
    private static readonly HashSet<string> _disabledEnemies
        = new HashSet<string> { "ClaySurgeon", "CaveDweller" };
    private static readonly bool _noBarber = RemoveTheAnnoyingBase.Instance.BarberDisabled.Value;
    private static readonly bool _noManeater = RemoveTheAnnoyingBase.Instance.ManeaterDisabled.Value;

    private static void Prefix(SelectableLevel newLevel) {
        try { LevelOperation(newLevel, false); } catch (Exception ex) {
            _log.LogWarning($"Prefix disabling ran incorrectly: {ex}");
        }
    }

    private static void Postfix(SelectableLevel newLevel) {
        try { LevelOperation(newLevel, true); } catch (Exception ex) {
            _log.LogWarning($"Postfix disabling ran incorrectly: {ex}");
        }
    }

    private static void LevelOperation(SelectableLevel newLevel, bool log) {
        // Check for company
        if (SelectableLevelIsCompany(newLevel)) return;

        // Check if the user is ok with both enemies
        if (!_noBarber && !_noManeater) {
            if (log) _log.LogInfo("All unfun enemies allowed by user config.");
            return;
        }

        // Check if the level contains any of the disabled enemies
        if (!newLevel.Enemies.Any(e => _disabledEnemies.Contains(e.enemyType.name))) {
            if (log) _log.LogInfo("No unfun enemies detected in spawning pool.");
            return;
        }

        // Check and count disabled enemies, modify along the way
        int disabledCount = 0;
        foreach (SpawnableEnemyWithRarity e in newLevel.Enemies) {
            if (DisableEnemyIfStinky(e, log)) disabledCount++;
        }
        if (log) _log.LogInfo($"Disabled {disabledCount} unfun enemies in current level.");
        if (log && disabledCount > 0) _log.LogDebug("Level will not spawn any unfun enemies.");
    }

    private static bool DisableEnemyIfStinky(SpawnableEnemyWithRarity enemy, bool log) {
        string enemyName = enemy.enemyType.name;
        if (_disabledEnemies.Contains(enemyName)) {
            // Check to see if the user is ok with the barber
            if (enemyName.Equals("ClaySurgeon") && !_noBarber) {
                if (log) _log.LogInfo("Barber allowed due to user config.");
                return false;
            }

            // Check to see if the user is ok with the maneater
            if (enemyName.Equals("CaveDweller") && !_noManeater) {
                if (log) _log.LogInfo("Maneater allowed due to user config");
                return false;
            }

            enemy.rarity = 0;
            enemy.enemyType.spawningDisabled = true;
            if (log) _log.LogInfo($"Spawning of {enemyName} disabled.");
            return true;
        }
        return false;
    }

    private static bool SelectableLevelIsCompany(SelectableLevel selectableLevel) {
        string levelName = selectableLevel.name.Replace("Level", "");
        return levelName.Equals("CompanyBuilding");
    }
}
