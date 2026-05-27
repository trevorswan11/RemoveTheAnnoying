using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RemoveTheAnnoying.Patches
{
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
}
