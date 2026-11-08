namespace BetterTask.Utils;

public static class ThemePresetCatalog
{
    private static readonly string[] builtInNames = { "Midnight", "Ocean", "Ember", "Forest", "Rose" };

    public static IReadOnlyList<string> BuiltInNames => builtInNames;

    public static ThemePreset CreateBuiltIn(string name)
    {
        ThemeSettings theme = new();
        switch (name)
        {
            case "Midnight":
                theme.UseBackgroundGradient = true;
                theme.BackgroundGradientStart = "#FF0B1017";
                theme.BackgroundGradientEnd = "#FF19152D";
                theme.UseAccentGradient = true;
                theme.AccentGradientStart = "#FF7569E8";
                theme.AccentGradientEnd = "#FF4E63D8";
                break;
            case "Ocean":
                SetColors(theme, new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#FF07151D",
                    ["SurfaceBrush"] = "#E60B2632",
                    ["CardBrush"] = "#E6103340",
                    ["CardHoverBrush"] = "#F0174555",
                    ["CardPressedBrush"] = "#FF1A5260",
                    ["OutlineBrush"] = "#FF1C4B5B",
                    ["OutlineHoverBrush"] = "#FF337183",
                    ["TextPrimaryBrush"] = "#FFE7FAFF",
                    ["TextSecondaryBrush"] = "#FF90BBC5",
                    ["AccentBrush"] = "#FF2EC4B6",
                    ["AccentHoverBrush"] = "#FF50D9CB",
                    ["AccentPressedBrush"] = "#FF1B9E97",
                    ["AccentSoftBrush"] = "#DD163F46",
                    ["SuccessBrush"] = "#FF68D8A2",
                    ["DangerBrush"] = "#FFE46878",
                    ["DangerSurfaceBrush"] = "#DD47242D",
                    ["DangerHoverBrush"] = "#FF60303A",
                    ["DangerTextBrush"] = "#FFFFE9EC"
                });
                SetGradients(theme, "#FF06151F", "#FF102E3D", "#FF2EC4B6", "#FF4387E8");
                break;
            case "Ember":
                SetColors(theme, new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#FF170D0B",
                    ["SurfaceBrush"] = "#E6241411",
                    ["CardBrush"] = "#E62E1914",
                    ["CardHoverBrush"] = "#F03D2118",
                    ["CardPressedBrush"] = "#FF4C281B",
                    ["OutlineBrush"] = "#FF593326",
                    ["OutlineHoverBrush"] = "#FF805039",
                    ["TextPrimaryBrush"] = "#FFFFF2E8",
                    ["TextSecondaryBrush"] = "#FFC8A38C",
                    ["AccentBrush"] = "#FFFF8A45",
                    ["AccentHoverBrush"] = "#FFFFA367",
                    ["AccentPressedBrush"] = "#FFE86B2F",
                    ["AccentSoftBrush"] = "#DD4E291B",
                    ["SuccessBrush"] = "#FF79C990",
                    ["DangerBrush"] = "#FFE85E60",
                    ["DangerSurfaceBrush"] = "#DD512522",
                    ["DangerHoverBrush"] = "#FF74352B",
                    ["DangerTextBrush"] = "#FFFFE7DF"
                });
                SetGradients(theme, "#FF170D0B", "#FF351914", "#FFFF8A45", "#FFE85B45");
                break;
            case "Forest":
                SetColors(theme, new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#FF09140F",
                    ["SurfaceBrush"] = "#E6122119",
                    ["CardBrush"] = "#E6192B20",
                    ["CardHoverBrush"] = "#F022392A",
                    ["CardPressedBrush"] = "#FF2B4934",
                    ["OutlineBrush"] = "#FF2D4936",
                    ["OutlineHoverBrush"] = "#FF4A7050",
                    ["TextPrimaryBrush"] = "#FFEAF5E9",
                    ["TextSecondaryBrush"] = "#FFA0B8A4",
                    ["AccentBrush"] = "#FF75C879",
                    ["AccentHoverBrush"] = "#FF91D995",
                    ["AccentPressedBrush"] = "#FF56A85E",
                    ["AccentSoftBrush"] = "#DD1D4029",
                    ["SuccessBrush"] = "#FF75D5A1",
                    ["DangerBrush"] = "#FFE36A70",
                    ["DangerSurfaceBrush"] = "#DD47242D",
                    ["DangerHoverBrush"] = "#FF60303A",
                    ["DangerTextBrush"] = "#FFFFE8E9"
                });
                SetGradients(theme, "#FF09140F", "#FF162D20", "#FF75C879", "#FF48A89A");
                break;
            case "Rose":
                SetColors(theme, new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#FF160D18",
                    ["SurfaceBrush"] = "#E6231425",
                    ["CardBrush"] = "#E62D1A30",
                    ["CardHoverBrush"] = "#F03C2440",
                    ["CardPressedBrush"] = "#FF4B2D50",
                    ["OutlineBrush"] = "#FF563858",
                    ["OutlineHoverBrush"] = "#FF815782",
                    ["TextPrimaryBrush"] = "#FFFFF0FA",
                    ["TextSecondaryBrush"] = "#FFC4A3BE",
                    ["AccentBrush"] = "#FFDE6CA8",
                    ["AccentHoverBrush"] = "#FFEB8CBD",
                    ["AccentPressedBrush"] = "#FFC95291",
                    ["AccentSoftBrush"] = "#DD482342",
                    ["SuccessBrush"] = "#FF70C6A0",
                    ["DangerBrush"] = "#FFEA667E",
                    ["DangerSurfaceBrush"] = "#DD51253A",
                    ["DangerHoverBrush"] = "#FF74354F",
                    ["DangerTextBrush"] = "#FFFFE9F0"
                });
                SetGradients(theme, "#FF160D18", "#FF30182C", "#FFDE6CA8", "#FF9F6BDB");
                break;
            default:
                throw new ArgumentException($"Unknown built-in theme '{name}'.", nameof(name));
        }

        theme.Normalize();
        return new ThemePreset { Name = name, Theme = theme };
    }

    private static void SetColors(ThemeSettings theme, Dictionary<string, string> colors)
    {
        foreach ((string key, string value) in colors)
            theme.Colors[key] = value;
    }

    private static void SetGradients(
        ThemeSettings theme,
        string backgroundStart,
        string backgroundEnd,
        string accentStart,
        string accentEnd)
    {
        theme.UseBackgroundGradient = true;
        theme.BackgroundGradientStart = backgroundStart;
        theme.BackgroundGradientEnd = backgroundEnd;
        theme.UseAccentGradient = true;
        theme.AccentGradientStart = accentStart;
        theme.AccentGradientEnd = accentEnd;
    }
}
