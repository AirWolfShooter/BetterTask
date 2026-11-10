using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BetterTask.Core;
using BetterTask.Core.Input;
using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Interface;
using BetterTask.Interface.UserControlPages;
using SharpHook;
using SharpHook.Data;
using static BetterTask.Interface.UserControlPages.KeybindsSetting;

namespace BetterTask.Interface.Managers;

public class KeybindsManager
{
    private static readonly KeybindsManager instance = new();
    public static KeybindsManager Instance => instance;

    public Dictionary<KeybindType, KeyCode> Hotkeys { get; set; } = new();
    public Playback playback = Playback.Instance;
    private readonly Dictionary<KeybindType, Action> Actions = new();

    public void startListening()
    {
        loadKeybinds();
        Actions[KeybindType.Recording] = () =>
        {
            MainWindow.loop = false;
            MainWindow.Instance.recordingClicked(null, null);
        };
        Actions[KeybindType.AppendRecording] = () =>
            MainWindow.Instance.appendRecordingClicked(null, null);
        Actions[KeybindType.PauseRecording] = () => MainWindow.Instance.PauseRecording();
        Actions[KeybindType.ResumeRecording] = () => MainWindow.Instance.ResumeRecording();
        Actions[KeybindType.Playback] = () => MainWindow.Instance.playbackClicked(null, null);
        Actions[KeybindType.PausePlayback] = playback.pausePlayback;
        Actions[KeybindType.StopPlayback] = playback.stopPlayback;
        Actions[KeybindType.ResumePlayback] = playback.resumePlayback;
        Actions[KeybindType.LoopPlayback] = () =>
        {
            MainWindow.loop = true;
            MainWindow.Instance.playbackClicked(null, null);
        };

        GlobalHookManager.Instance.KeyPressed += OnKeyPress;
    }

    public void loadKeybinds()
    {
        Hotkeys = SettingsManager.Instance.settings.Hotkeys ?? new();
    }

    public void stopListening()
    {
        GlobalHookManager.Instance.KeyPressed -= OnKeyPress;
    }

    private void OnKeyPress(object? sender, KeyboardHookEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.Windows.OfType<Settings>().Any())
            {
                return;
            }

            foreach (var hotkey in Hotkeys.ToList())
            {
                if (!KeybindsSetting.pickingKeybind && hotkey.Value == e.Data.KeyCode)
                {
                    if (Actions.TryGetValue(hotkey.Key, out Action? action))
                    {
                        action();
                    }

                    break;
                }
            }
        });
    }
}
