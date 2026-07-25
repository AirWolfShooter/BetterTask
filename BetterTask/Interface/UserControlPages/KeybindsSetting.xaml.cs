using BetterTask.Core;
using BetterTask.Core.RecordingAndPlayback;
using SharpHook;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BetterTask.Interface.UserControlPages
{
    /// <summary>
    /// Interaction logic for KeybindsSetting.xaml
    /// </summary>
    public partial class KeybindsSetting : UserControl
    {
        public KeybindsSetting()
        {
            InitializeComponent();
            Loaded += KeybindsSetting_Loaded;
        }

        private void KeybindsSetting_Loaded(object sender, RoutedEventArgs e)
        {
            LoadKeybindButtons();
        }

        public static bool pickingKeybind;

        private TaskCompletionSource<KeyCode?>? keyWaiter;

        public enum KeybindType
        {
            Recording,
            Playback,
            PausePlayback,
            StopPlayback,
            ResumePlayback,
            LoopPlayback
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject obj)
        where T : DependencyObject
        {
            if (obj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        public static Dictionary<KeybindType, KeyCode> actions = new();

        public Task<KeyCode?> GetKey()
        {
            keyWaiter = new TaskCompletionSource<KeyCode?>();

            GlobalHookManager.Instance.KeyReleased += OnKeyUp;

            return keyWaiter.Task;
        }

        private void LoadKeybindButtons()
        {
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                if (button.Tag == null)
                    continue;

                if (Enum.TryParse(button.Tag.ToString(), out KeybindType keybindType))
                {
                    if (KeybindsManager.Instance.Hotkeys.TryGetValue(keybindType, out KeyCode key))
                    {
                        button.Content = key.ToString().Replace("Vc", "");
                    }
                    else
                    {
                        button.Content = "None";
                    }
                }
            }
        }

        private void OnKeyUp(object? sender, KeyboardHookEventArgs e)
        {
            var key = e.Data.KeyCode;

            Dispatcher.Invoke(() =>
            {
                keyWaiter?.SetResult(key);
                GlobalHookManager.Instance.KeyReleased -= OnKeyUp;
            });
        }

        private async void SetKey(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            KeybindType keybindType = Enum.Parse<KeybindType>(button.Tag!.ToString()!);

            button.Content = "Press a key...";
            pickingKeybind = true;

            KeyCode? key = await GetKey();

            if (!key.HasValue)
            {
                pickingKeybind = false;
                return;
            }

            if (key.Value == KeyCode.VcEscape)
            {
                if (KeybindsManager.Instance.Hotkeys.TryGetValue(keybindType, out KeyCode currentKey))
                    button.Content = currentKey.ToString().Replace("Vc", "");
                else
                {
                    button.Content = "None";
                    pickingKeybind = false;
                    return;
                }
            }

            if (key.Value == KeyCode.VcSlash || key.Value == KeyCode.VcBackslash)
            {
                KeybindsManager.Instance.Hotkeys.Remove(keybindType);
                SettingsManager.Instance.settings.Hotkeys = KeybindsManager.Instance.Hotkeys;

                LoadKeybindButtons();

                pickingKeybind = false;

                if (SettingsManager.Instance.settings.SaveAutomatically ?? false)
                {
                    SettingsManager.Instance.saveSettings();
                }

                return;
            }

            KeybindType? previousAction = null;

            foreach (var pair in KeybindsManager.Instance.Hotkeys)
            {
                if (pair.Key != keybindType && pair.Value == key.Value)
                {
                    previousAction = pair.Key;
                    break;
                }
            }

            if (previousAction.HasValue)
            {
                KeybindsManager.Instance.Hotkeys.Remove(previousAction.Value);
            }

            KeybindsManager.Instance.Hotkeys[keybindType] = key.Value;
            SettingsManager.Instance.settings.Hotkeys = KeybindsManager.Instance.Hotkeys;

            if (SettingsManager.Instance.settings.SaveAutomatically ?? false)
            {
                SettingsManager.Instance.saveSettings();
            }

            LoadKeybindButtons();

            pickingKeybind = false;

            System.Diagnostics.Debug.WriteLine($"Set {keybindType} to {key.Value}");
        }
    }
}
