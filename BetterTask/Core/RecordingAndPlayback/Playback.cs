using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Utils;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

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
            if (!playbackStarted || playbackPaused)
                return;

            playbackPaused = true;
            pausedEvent.Reset();
            AppLogger.Info("Playback", "Playback paused.");
        }

        public void resumePlayback()
        {
            if (!playbackStarted || !playbackPaused)
                return;

            playbackPaused = false;
            pausedEvent.Set();
            AppLogger.Info("Playback", "Playback resumed.");
        }

        public void stopPlayback()
        {
            if (!playbackStarted)
                return;

            playbackStopped = true;
            pausedEvent.Set();
            AppLogger.Info("Playback", "Playback stop requested.");
        }

        public Task StartPlayback(bool loop = false)
        {
            return StartPlayback(Recording.RecordedEvents, loop);
        }

        public Task StartPlayback(IReadOnlyList<RecordedEvent> events, bool loop = false)
        {
            if (playbackStarted)
                return Task.CompletedTask;

            if (events.Count == 0)
                return Task.CompletedTask;

            List<RecordedEvent> eventsToPlay = events.ToList();
            playbackStarted = true;
            playbackStopped = false;
            playbackPaused = false;
            pausedEvent.Set();
            AppLogger.Info("Playback", $"Started playback of {eventsToPlay.Count:N0} events; loop={loop}.");

            // Start the display timer before scheduling the playback worker so the
            // first event does not wait on a worker-thread-to-UI-thread round trip.
            Dispatcher.UIThread.Invoke(() => MainWindow.Instance.resetTimer());

            return Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        Play(eventsToPlay);

                        if (loop && !playbackStopped)
                        {
                            int delay = SettingsManager.Instance.settings.StartDelay ?? 0;

                            if (delay > 0)
                                await Task.Delay(delay);
                        }
                    }
                    while (loop && !playbackStopped);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Playback", "Playback failed.", ex);
                    throw;
                }
                finally
                {
                    AppLogger.Info("Playback", playbackStopped ? "Playback stopped." : "Playback completed.");
                    playbackStarted = false;
                    playbackPaused = false;

                    Dispatcher.UIThread.Invoke(() =>
                    {
                        MainWindow.displayTimer.Stop();
                    });
                }
            });
        }

        public void Play(IReadOnlyList<RecordedEvent> events)
        {
            Stopwatch timer = Stopwatch.StartNew();
            long pausedTime = 0;
            long firstEventTime = events.Count > 0 ? events[0].Time : 0;

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

                    // Event timestamps include the time spent between pressing
                    // Record and making the first input. Skip that leading
                    // silence, but preserve every delay within the recording.
                    long eventTime = Math.Max(0, recordedEvent.Time - firstEventTime);
                    long remaining = eventTime - (timer.ElapsedMilliseconds - pausedTime);

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

                    if (recordedEvent.MouseX.HasValue && recordedEvent.MouseY.HasValue)
                    {
                        simulator.SimulateMouseMovement(
                            recordedEvent.MouseX.Value,
                            recordedEvent.MouseY.Value
                        );
                    }

                    simulator.SimulateMouseWheel(
                        recordedEvent.Rotation!.Value,
                        MouseWheelScrollDirection.Vertical,
                        MouseWheelScrollType.UnitScroll
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
