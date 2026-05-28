namespace RemoveTheAnnoying.Util;

public class Level {
    public static string StripLevelSuffix(string name) => name.Replace("Level", "");

    public static bool Is(SelectableLevel level, string name) =>
        StripLevelSuffix(level.name) == name;

    public static bool IsCompanyBuilding(SelectableLevel level) => Is(level, "CompanyBuilding");
}
