using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BetterTask.Core;
using BetterTask.Interface.Managers;
using BetterTask.Utils;
using SharpHook;
using SharpHook.Data;

namespace BetterTask.Interface.UserControlPages;

public partial class KeybindsSetting : UserControl
{
    public KeybindsSetting()
    {
        InitializeComponent();
        Loaded += KeybindsSetting_Loaded;
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
        LoopPlayback,
        AppendRecording,
        PauseRecording,
        ResumeRecording
    }

    public static Dictionary<KeybindType, KeyCode> actions = new();

    public Task<KeyCode?> GetKey()
    {
        keyWaiter = new TaskCompletionSource<KeyCode?>();
        GlobalHookManager.Instance.KeyReleased += OnKeyUp;
        return keyWaiter.Task;
    }

    private static void LoadKeybinds(Button button)
    {
        if (button.Tag is null)
        {
            button.Content = "None";
            return;
        }

        if (Enum.TryParse(button.Tag.ToString(), out KeybindType keybindType))
        {
            button.Content = KeybindsManager.Instance.Hotkeys.TryGetValue(keybindType, out KeyCode key)
                ? key.ToString().Replace("Vc", "")
                : "None";
        }
    }

    private void LoadKeybindButtons()
    {
        Button[] buttons =
        {
            RecordHotkeyButton,
            AppendRecordingHotkeyButton,
            PauseRecordingHotkeyButton,
            ResumeRecordingHotkeyButton,
            PlaybackHotkeyButton,
            StopPlaybackHotkeyButton,
            PausePlaybackHotkeyButton,
            ResumePlaybackHotkeyButton,
            LoopPlaybackHotkeyButton
        };

        foreach (Button button in buttons)
        {
            LoadKeybinds(button);
        }
    }

    private void KeybindsSetting_Loaded(object? sender, RoutedEventArgs e)
    {
        LoadKeybindButtons();
    }

    private void OnKeyUp(object? sender, KeyboardHookEventArgs e)
    {
        KeyCode key = e.Data.KeyCode;
        Dispatcher.UIThread.Invoke(() =>
        {
            keyWaiter?.TrySetResult(key);
            GlobalHookManager.Instance.KeyReleased -= OnKeyUp;
        });
    }

    private async void SetKey(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

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
            {
                button.Content = currentKey.ToString().Replace("Vc", "");
            }
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
        AppLogger.Info("Keybinds", $"Assigned {keybindType} to {key.Value}.");
    }
}
