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
        public Dictionary<KeybindType, KeyCode>? Hotkeys { get; set; }
    }
}
