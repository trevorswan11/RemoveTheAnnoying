using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RemoveTheAnnoying.Patches
{
    [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
    public class EclipsedScrapValuePatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly float Multiplier = RemoveAnnoyingBase.Instance.EclipsedMultiplier.Value;

        public static void Prefix(RoundManager __instance)
        {
            if (Multiplier <= 0 || Multiplier > 2 || Multiplier == 1)
            {
                Logger.LogInfo($"Given multiplier was {Multiplier}, I won't proceed.");
                return;
            }

            if ((int)TimeOfDay.Instance.currentLevelWeather == 5) typeof(RoundManager).GetField("scrapValueMultiplier").SetValue(__instance, Multiplier);
            Logger.LogInfo($"I set the spawned scrap multiplier to {Multiplier} successfully.");
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "SetPlanetsWeather")]
    public class WeatherPatch
    {
        private static readonly ManualLogSource Logger = RemoveAnnoyingBase.mls;
        private static readonly string ForceWeather = RemoveAnnoyingBase.Instance.ForceWeather.Value;
        private static readonly string DisableWeather = RemoveAnnoyingBase.Instance.DisableWeather.Value;
        private static readonly Dictionary<string, LevelWeatherType[]> MoonWeathers = new Dictionary<string, LevelWeatherType[]>();
        private static readonly LevelWeatherType[] AllWeathers = new LevelWeatherType[]
            {
                LevelWeatherType.None,
                LevelWeatherType.Rainy,
                LevelWeatherType.Stormy,
                LevelWeatherType.Foggy,
                LevelWeatherType.Flooded,
                LevelWeatherType.Eclipsed,
            };
        private static readonly int MaximumRerollAttempts = 100;

        private static bool Prefix(SelectableLevel[] ___levels)
        {
            Logger.LogInfo("Performing weather tweaks");
            // If ForceWeather has some valid value, and is not disabled
            if (ForceWeather != null && !(ForceWeather.Equals("disabled")))
            {
                // Parse force weather type and force all planets
                LevelWeatherType? type = ConvertConfigEntryToWeatherType(ForceWeather);
                if (type != null) return ForceAllPlanetsWeather(___levels, (LevelWeatherType)type);
                else
                {
                    Logger.LogInfo("ForceWeather input was not identified.");
                    return true;
                }
            }

            // If forceweather is null or is disabled, we can try to restrict weather with disableweather
            if (DisableWeather != null && !(DisableWeather.Equals("disabled")))
            {
                LevelWeatherType? type = ConvertConfigEntryToWeatherType(DisableWeather);
                if (type != null) return PreventWeatherGeneration(___levels, (LevelWeatherType)type);
                else
                {
                    Logger.LogInfo("DisableWeather input was not identified.");
                    return true;
                }
            }
            return true;
        }

        private static LevelWeatherType? ConvertConfigEntryToWeatherType(string value)
        {
            switch (value.ToLower())
            {
                case "none":
                    return LevelWeatherType.None;
                case "rainy":
                    return LevelWeatherType.Rainy;
                case "stormy":
                    return LevelWeatherType.Stormy;
                case "foggy":
                    return LevelWeatherType.Foggy;
                case "flooded":
                    return LevelWeatherType.Flooded;
                case "eclipsed":
                    return LevelWeatherType.Eclipsed;
                default:
                    return null;
            }
        }

        private static bool ForceAllPlanetsWeather(SelectableLevel[] levels, LevelWeatherType forcedType)
        {
            foreach (SelectableLevel level in levels)
            {
                Logger.LogInfo($"Setting {level.name.Replace("Level", "")}'s weather to {ForceWeather}.");
                level.currentWeather = forcedType;
            }
            Logger.LogInfo($"All level's weather set to {ForceWeather}.");
            return false;
        }

        private static bool PreventWeatherGeneration(SelectableLevel[] levels, LevelWeatherType restrictedType)
        {
            MoonWeathersDictionary();
            foreach (SelectableLevel level in levels)
            {
                string levelName = level.name.Replace("Level", "");
                if (levelName.Equals("CompanyBuilding")) continue;
                try
                {
                    HashSet<LevelWeatherType> alloweableWeathers = new HashSet<LevelWeatherType>(MoonWeathers[levelName]);
                    for (int i = 0; i < MaximumRerollAttempts; i++)
                    {
                        if ((LevelWeatherType)level.currentWeather == restrictedType)
                        {
                            level.currentWeather = alloweableWeathers.ElementAt(new Random().Next(alloweableWeathers.Count));
                            Logger.LogDebug($"Rerolled level {levelName} to type {level.currentWeather}.");
                        }
                        else break;
                    }
                    Logger.LogInfo($"Finished rerolling level {levelName}: Final type = {level.currentWeather}.");
                }

                catch { Logger.LogDebug($"Error altering weather of type {restrictedType} on {levelName}."); }
            }
            return false;
        }

        private static void MoonWeathersDictionary()
        {
            if (MoonWeathers.ContainsKey("Artifice")) return;
            MoonWeathers.Add("CompanyBuilding", new LevelWeatherType[] { LevelWeatherType.None });
            MoonWeathers.Add("Experimentation", AllWeathers);
            MoonWeathers.Add("Assurance", AllWeathers);
            MoonWeathers.Add("Vow", ExcludeWeatherType(LevelWeatherType.Rainy));
            MoonWeathers.Add("Offense", ExcludeWeatherType(LevelWeatherType.Foggy));
            MoonWeathers.Add("March", ExcludeWeatherType(LevelWeatherType.Rainy));
            MoonWeathers.Add("Adamance", AllWeathers);
            MoonWeathers.Add("Rend", new LevelWeatherType[] { LevelWeatherType.Stormy, LevelWeatherType.Eclipsed });
            MoonWeathers.Add("Dine", ExcludeWeatherType(LevelWeatherType.Stormy, LevelWeatherType.Foggy));
            MoonWeathers.Add("Titan", ExcludeWeatherType(LevelWeatherType.Rainy, LevelWeatherType.Flooded));
            MoonWeathers.Add("Artifice", ExcludeWeatherType(LevelWeatherType.Foggy));
            MoonWeathers.Add("Embrion", new LevelWeatherType[] { LevelWeatherType.Foggy, LevelWeatherType.Eclipsed });
        }

        private static LevelWeatherType[] ExcludeWeatherType(params LevelWeatherType[] excludes)
        {
            HashSet<LevelWeatherType> excludeSet = new HashSet<LevelWeatherType>(excludes);
            List<LevelWeatherType> filtered = new List<LevelWeatherType>();
            foreach (LevelWeatherType weather in AllWeathers)
            {
                if (!excludeSet.Contains(weather)) filtered.Add(weather);
            }
            return filtered.ToArray();
        }
    }
}
