namespace BetterTask.Themes;

public sealed class ThemePreset
{
    public string Name { get; set; } = "Custom theme";
    public ThemeSettings Theme { get; set; } = new();

    public ThemePreset Clone(string? name = null)
    {
        return new ThemePreset
        {
            Name = name ?? Name,
            Theme = Theme.Clone()
        };
    }
}

public sealed class ThemePresetDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "Imported theme";
    public ThemeSettings? Theme { get; set; } = new();
    public string? BackgroundImageFileName { get; set; }
    public string? BackgroundImageData { get; set; }
}
