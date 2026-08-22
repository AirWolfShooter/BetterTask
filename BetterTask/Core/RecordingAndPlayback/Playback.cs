using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Interface.Utils;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BetterTask.Core.RecordingAndPlayback
{
    public class Playback
    {
        private static readonly Playback instance = new Playback();
        public static Playback Instance => instance;

        private readonly EventSimulator simulator = new EventSimulator();
        public static bool playbackStarted;
        public static bool playbackStopped;
        public static bool playbackPaused;
        private ManualResetEventSlim pausedEvent = new ManualResetEventSlim(true);

        private Playback() { }

        public void pausePlayback()
        {
            playbackPaused = true;
            pausedEvent.Reset();
        }

        public void resumePlayback()
        {
            playbackPaused = false;
            pausedEvent.Set();
        }

        public void stopPlayback()
        {
            playbackStopped = true;
            pausedEvent.Set();
        }

        public Task StartPlayback(bool loop = false)
        {
            if (playbackStarted)
                return Task.CompletedTask;

            playbackStarted = true;
            playbackStopped = false;
            playbackPaused = false;
            pausedEvent.Set();

            return Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CompositionTarget.Rendering -= MainWindow.Instance.UpdateTimer;
                            CompositionTarget.Rendering += MainWindow.Instance.UpdateTimer;
                            MainWindow.Instance.resetTimer();
                            MainWindow.displayTimer.Restart();
                        });

                        Play(Recording.RecordedEvents);

                        if (loop && !playbackStopped)
                        {
                            int delay = SettingsManager.Instance.settings.StartDelay ?? 0;

                            if (delay > 0)
                                await Task.Delay(delay);
                        }
                    }
                    while (loop && !playbackStopped);
                }
                finally
                {
                    playbackStarted = false;
                    playbackPaused = false;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CompositionTarget.Rendering -= MainWindow.Instance.UpdateTimer;
                        MainWindow.displayTimer.Stop();
                    });
                }
            });
        }

        public void Play(List<RecordedEvent> events)
        {
            Stopwatch timer = Stopwatch.StartNew();
            long pausedTime = 0;

            foreach (RecordedEvent recordedEvent in events)
            {
                if (playbackStopped)
                    return;

                while (true)
                {
                    if (playbackStopped)
                        return;

                    if (playbackPaused)
                    {
                        long pauseStart = timer.ElapsedMilliseconds;
                        pausedEvent.Wait();
                        pausedTime += timer.ElapsedMilliseconds - pauseStart;
                    }

                    long remaining = recordedEvent.Time - (timer.ElapsedMilliseconds - pausedTime);

                    if (remaining <= 0)
                        break;

                    if (remaining > 2)
                        Thread.Sleep(1);
                    else
                        Thread.SpinWait(100);
                }

                ExecuteEvent(recordedEvent);
            }
        }

        private void ExecuteEvent(RecordedEvent recordedEvent)
        {
            switch (recordedEvent.Type)
            {
                case EventType.MouseMoved:

                    simulator.SimulateMouseMovement(
                        recordedEvent.MouseX!.Value,
                        recordedEvent.MouseY!.Value
                    );
                    break;

                case EventType.MousePressed:

                    simulator.SimulateMousePress(
                        recordedEvent.MouseButton!.Value
                    );
                    break;

                case EventType.MouseReleased:

                    simulator.SimulateMouseRelease(
                        recordedEvent.MouseButton!.Value
                    );
                    break;

                case EventType.MouseWheel:

                    simulator.SimulateMouseWheel(
                        recordedEvent.Rotation!.Value,
                        recordedEvent.ScrollDirection!.Value,
                        recordedEvent.ScrollType!.Value
                    );
                    break;

                case EventType.KeyPressed:

                    simulator.SimulateKeyPress(
                        recordedEvent.KeyCode!.Value
                    );
                    break;

                case EventType.KeyReleased:

                    simulator.SimulateKeyRelease(
                        recordedEvent.KeyCode!.Value
                    );
                    break;
            }
        }
    }
}