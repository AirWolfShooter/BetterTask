using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Interface.UserControlPages;
using BetterTask.Utils;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BetterTask.Core.RecordingAndPlayback
{
    public class Recording
    {
        private static readonly Recording instance = new Recording();
        public static Recording Instance => instance;

        public static List<RecordedEvent> RecordedEvents = new();

        private HashSet<KeyCode> pressedKeys = new();

        private int lastMouseX;
        private int lastMouseY;

        private const int mouseMoveThreshold = 1;

        private readonly Stopwatch timer = new Stopwatch();
        private long recordingTimeOffset;
        public long RecordingTime => timer.ElapsedMilliseconds;

        public static bool isRecording;
        public static bool isPaused;


        public void Subscribe(bool appendToExisting = false)
        {
            if (isRecording)
                return;

            recordingTimeOffset = appendToExisting && RecordedEvents.Count > 0
                ? Math.Max(0, RecordedEvents[^1].Time)
                : 0;
            isRecording = true;
            isPaused = false;

            GlobalHookManager.Instance.MouseMoved += onMouseMove;
            GlobalHookManager.Instance.MouseClicked += onMouseClick;
            GlobalHookManager.Instance.MousePressed += onMouseDown;
            GlobalHookManager.Instance.MouseReleased += onMouseUp;
            GlobalHookManager.Instance.MouseWheel += onMouseWheel;
            GlobalHookManager.Instance.KeyPressed += onKeyDown;
            GlobalHookManager.Instance.KeyReleased += onKeyUp;
            MainWindow.Instance.resetTimer();

            timer.Restart();

            pressedKeys.Clear();
            AppLogger.Info(
                "Recording",
                appendToExisting
                    ? $"Started appended capture after {RecordedEvents.Count:N0} existing events."
                    : "Started a new capture.");
        }

        private void AddEvent(RecordedEvent recordedEvent)
        {
            if (isPaused)
                return;

            recordedEvent.Time += recordingTimeOffset;
            RecordedEvents.Add(recordedEvent);
        }

        public void onMouseMove(object? sender, MouseHookEventArgs e)
        {
            int distanceX = Math.Abs(e.Data.X - lastMouseX);
            int distanceY = Math.Abs(e.Data.Y - lastMouseY);

            if (distanceX < mouseMoveThreshold && distanceY < mouseMoveThreshold)
                return;

            lastMouseX = e.Data.X;
            lastMouseY = e.Data.Y;

            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MouseMoved,
                MouseX = e.Data.X,
                MouseY = e.Data.Y
            });
        }

        public void onMouseClick(object? sender, MouseHookEventArgs e)
        {
            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MouseClicked,
                MouseX = e.Data.X,
                MouseY = e.Data.Y,
                MouseButton = e.Data.Button
            });
        }

        public void onMouseDown(object? sender, MouseHookEventArgs e)
        {
            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MousePressed,
                MouseX = e.Data.X,
                MouseY = e.Data.Y,
                MouseButton = e.Data.Button
            });
        }

        public void onMouseUp(object? sender, MouseHookEventArgs e)
        {
            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MouseReleased,
                MouseX = e.Data.X,
                MouseY = e.Data.Y,
                MouseButton = e.Data.Button
            });
        }

        public void onMouseWheel(object? sender, MouseWheelHookEventArgs e)
        {
            int rotationAsInt = e.Data.Rotation/3;
            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MouseWheel,
                MouseX = e.Data.X,
                MouseY = e.Data.Y,
                Rotation = (short)rotationAsInt,
                WheelDelta = e.Data.Delta,
                ScrollType = e.Data.Type,
                ScrollDirection = e.Data.Direction
            });
        }

        public void onKeyDown(object? sender, KeyboardHookEventArgs e)
        {
            if (pressedKeys.Contains(e.Data.KeyCode) || KeybindsManager.Instance.Hotkeys.ContainsValue(e.Data.KeyCode))
                return;

            pressedKeys.Add(e.Data.KeyCode);

            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.KeyPressed,
                KeyCode = e.Data.KeyCode
            });
        }

        public void onKeyUp(object? sender, KeyboardHookEventArgs e)
        {
            pressedKeys.Remove(e.Data.KeyCode);
            if (KeybindsManager.Instance.Hotkeys.ContainsValue(e.Data.KeyCode))
    return;

            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.KeyReleased,
                KeyCode = e.Data.KeyCode
            });
        }

        public void Pause()
        {
            if (!isRecording || isPaused)
                return;
            isPaused = true;
            timer.Stop();
            AppLogger.Info("Recording", "Recording paused.");
        }

        public void Resume()
        {
            if (!isRecording || !isPaused)
                return;
            isPaused = false;
            timer.Start();
            AppLogger.Info("Recording", "Recording resumed.");
        }

        public void Unsubscribe()
        {
            if (!isRecording)
                return;

            GlobalHookManager.Instance.MouseMoved -= onMouseMove;
            GlobalHookManager.Instance.MouseClicked -= onMouseClick;
            GlobalHookManager.Instance.MouseWheel -= onMouseWheel;
            GlobalHookManager.Instance.MousePressed -= onMouseDown;
            GlobalHookManager.Instance.MouseReleased -= onMouseUp;

            GlobalHookManager.Instance.KeyPressed -= onKeyDown;
            GlobalHookManager.Instance.KeyReleased -= onKeyUp;
            MainWindow.displayTimer.Stop();

            pressedKeys.Clear();

            AppLogger.Info(
                "Recording",
                $"Stopped capture after {timer.ElapsedMilliseconds:N0} ms with {RecordedEvents.Count:N0} total events.");
            timer.Stop();

            isRecording = false;
            isPaused = false;
        }

        public void clearRecording()
        {
            if (!isRecording)
            {
                int previousEventCount = RecordedEvents.Count;
                RecordedEvents.Clear();
                pressedKeys.Clear();

                lastMouseX = 0;
                lastMouseY = 0;
                recordingTimeOffset = 0;

                timer.Reset();
                if (previousEventCount > 0)
                    AppLogger.Info("Recording", $"Cleared the previous capture ({previousEventCount:N0} events).");
            }
        }

        private Recording() { }
    }
}
