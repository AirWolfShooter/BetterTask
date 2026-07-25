using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BetterTask.Core;
using BetterTask.Interface;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;

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
        }

        public Task StartPlayback(bool loop = false)
        {
            if (playbackStarted)
                return Task.CompletedTask;
            playbackStopped = false;

            return Task.Run(() =>
            {
                do
                {
                    Play(Recording.RecordedEvents);
                }
                while (loop && !playbackStopped);
                playbackStarted = false;
                playbackPaused = false;
            });
            
        }

        public void Play(List<RecordedEvent> events)
        {
            playbackStarted = true;

            Stopwatch timer = Stopwatch.StartNew();
            long pausedTime = 0;

            foreach (RecordedEvent recordedEvent in events)
            {
                if (playbackStopped) return;
                while (timer.ElapsedMilliseconds - pausedTime < recordedEvent.Time)
                {
                    if (playbackStopped) return;
                    if (playbackPaused)
                    {
                        long pauseStart = timer.ElapsedMilliseconds;
                        pausedEvent.Wait();
                        pausedTime += timer.ElapsedMilliseconds - pauseStart;
                    }

                    Thread.Sleep(1);
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