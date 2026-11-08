using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Text;
using static BetterTask.Interface.UserControlPages.KeybindsSetting;

namespace BetterTask.Utils
{
    public class AppSettings
    {
        public bool? StayOnTop { get; set; }
        public bool? CheckForUpdates { get; set; }
        public bool? SaveAutomatically { get; set; }
        public bool? CompactMode { get; set; }
        public int CompactStyle { get; set; }
        public bool ShowAppendRecordingButton { get; set; }
        public ThemeSettings? Theme { get; set; } = new();
        public List<ThemePreset> ThemePresets { get; set; } = new();
        public Dictionary<KeybindType, KeyCode>? Hotkeys { get; set; }

        public int? StartDelay { get; set; }
    }
}
