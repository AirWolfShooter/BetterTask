using BetterTask.Themes;
using SharpHook.Data;
using static BetterTask.Interface.UserControlPages.KeybindsSetting;

namespace BetterTask.Settings
{
    public class AppSettings
    {
        public bool? StayOnTop { get; set; }
        public bool? CheckForUpdates { get; set; }
        public bool? SaveAutomatically { get; set; }
        public bool? CompactMode { get; set; }
        public int CompactStyle { get; set; }
        public bool ShowAppendRecordingButton { get; set; }
        public string MacroShareApiUrl { get; set; } = string.Empty;
        public ThemeSettings? Theme { get; set; } = new();
        public List<ThemePreset> ThemePresets { get; set; } = new();
        public Dictionary<KeybindType, KeyCode>? Hotkeys { get; set; }

        public int? StartDelay { get; set; }
    }
}
