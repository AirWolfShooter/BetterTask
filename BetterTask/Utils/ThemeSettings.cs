namespace BetterTask.Utils;

public sealed class ThemeSettings
{
    private static readonly Dictionary<string, string> defaultColors = new()
    {
        ["AppBackgroundBrush"] = "#FF0B1017",
        ["SurfaceBrush"] = "#E6121A25",
        ["CardBrush"] = "#E6172230",
        ["CardHoverBrush"] = "#F0202E3E",
        ["CardPressedBrush"] = "#FF263448",
        ["OutlineBrush"] = "#FF29384A",
        ["OutlineHoverBrush"] = "#FF42566D",
        ["TextPrimaryBrush"] = "#FFF1F4F9",
        ["TextSecondaryBrush"] = "#FF9BA9B9",
        ["AccentBrush"] = "#FF7569E8",
        ["AccentHoverBrush"] = "#FF897DF4",
        ["AccentPressedBrush"] = "#FF6054D0",
        ["AccentSoftBrush"] = "#DD252345",
        ["SuccessBrush"] = "#FF53C69A",
        ["DangerBrush"] = "#FFE35E71",
        ["DangerSurfaceBrush"] = "#DD47242D",
        ["DangerHoverBrush"] = "#FF60303A",
        ["DangerTextBrush"] = "#FFFFDDE2"
    };

    public Dictionary<string, string> Colors { get; set; } = new(defaultColors);

    public bool UseBackgroundGradient { get; set; }
    public bool AnimateBackgroundGradient { get; set; }
    public string BackgroundGradientStart { get; set; } = "#FF0B1017";
    public string BackgroundGradientEnd { get; set; } = "#FF19152D";

    public bool UseAccentGradient { get; set; }
    public bool AnimateAccentGradient { get; set; }
    public string AccentGradientStart { get; set; } = "#FF7569E8";
    public string AccentGradientEnd { get; set; } = "#FF4E63D8";

    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageOpacity { get; set; } = 0.3;

    public static IReadOnlyDictionary<string, string> DefaultColors => defaultColors;

    public ThemeSettings Clone()
    {
        return new ThemeSettings
        {
            Colors = new Dictionary<string, string>(Colors ?? new Dictionary<string, string>()),
            UseBackgroundGradient = UseBackgroundGradient,
            AnimateBackgroundGradient = AnimateBackgroundGradient,
            BackgroundGradientStart = BackgroundGradientStart,
            BackgroundGradientEnd = BackgroundGradientEnd,
            UseAccentGradient = UseAccentGradient,
            AnimateAccentGradient = AnimateAccentGradient,
            AccentGradientStart = AccentGradientStart,
            AccentGradientEnd = AccentGradientEnd,
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageOpacity = BackgroundImageOpacity
        };
    }

    public void Normalize()
    {
        Colors ??= new Dictionary<string, string>();
        foreach ((string key, string defaultValue) in defaultColors)
        {
            if (!Colors.TryGetValue(key, out string? value) || !IsValidColor(value))
            {
                Colors[key] = defaultValue;
            }
        }

        if (!IsValidColor(BackgroundGradientStart))
            BackgroundGradientStart = defaultColors["AppBackgroundBrush"];
        if (!IsValidColor(BackgroundGradientEnd))
            BackgroundGradientEnd = "#FF19152D";
        if (!IsValidColor(AccentGradientStart))
            AccentGradientStart = defaultColors["AccentBrush"];
        if (!IsValidColor(AccentGradientEnd))
            AccentGradientEnd = "#FF4E63D8";

        BackgroundImageOpacity = Math.Clamp(BackgroundImageOpacity, 0, 1);
    }

    private static bool IsValidColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            _ = Avalonia.Media.Color.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
