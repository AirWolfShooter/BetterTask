using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Interface;
using BetterTask.Interface.UserControlPages;
using SharpHook;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;

using static BetterTask.Interface.UserControlPages.KeybindsSetting;

namespace BetterTask.Core
{
    public class KeybindsManager
    {
        private static readonly Lazy<KeybindsManager> _instance =
            new(() => new KeybindsManager());

        public static KeybindsManager Instance => _instance.Value;

        public Dictionary<KeybindType, KeyCode> Hotkeys { get; set; } = new();

        public Recording recording = Recording.Instance;
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
            Actions[KeybindType.Playback] = () =>
            {
                MainWindow.Instance.playbackClicked(null, null);
            };
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
            foreach (var hotkey in Hotkeys.ToList())
            {
                if (!KeybindsSetting.pickingKeybind && hotkey.Value == e.Data.KeyCode)
                {
                    if (Actions.TryGetValue(hotkey.Key, out Action? action))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            action();
                        });
                    }

                    break;
                }
            }
        }
    }
}