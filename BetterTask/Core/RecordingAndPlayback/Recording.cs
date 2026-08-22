using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Interface.UserControlPages;
using BetterTask.Interface.Utils;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

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

        private const int mouseMoveThreshold = 0;

        private readonly Stopwatch timer = new Stopwatch();
        public long RecordingTime => timer.ElapsedMilliseconds;

        public static bool isRecording;


        public void Subscribe()
        {
            GlobalHookManager.Instance.MouseMoved += onMouseMove;
            GlobalHookManager.Instance.MouseClicked += onMouseClick;
            GlobalHookManager.Instance.MousePressed += onMouseDown;
            GlobalHookManager.Instance.MouseReleased += onMouseUp;
            GlobalHookManager.Instance.MouseWheel += onMouseWheel;
            GlobalHookManager.Instance.KeyPressed += onKeyDown;
            GlobalHookManager.Instance.KeyReleased += onKeyUp;

            CompositionTarget.Rendering += MainWindow.Instance.UpdateTimer;
            MainWindow.Instance.resetTimer();

            timer.Restart();

            isRecording = true;

            pressedKeys.Clear();
        }

        private void AddEvent(RecordedEvent recordedEvent)
        {
            RecordedEvents.Add(recordedEvent);
            System.Diagnostics.Debug.WriteLine($"added event: {recordedEvent}");
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
            AddEvent(new RecordedEvent
            {
                Time = timer.ElapsedMilliseconds,
                Type = EventType.MouseWheel,
                Rotation = e.Data.Rotation,
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

        public void Unsubscribe()
        {
            GlobalHookManager.Instance.MouseMoved -= onMouseMove;
            GlobalHookManager.Instance.MouseClicked -= onMouseClick;
            GlobalHookManager.Instance.MouseWheel -= onMouseWheel;
            GlobalHookManager.Instance.MousePressed -= onMouseDown;
            GlobalHookManager.Instance.MouseReleased -= onMouseUp;

            GlobalHookManager.Instance.KeyPressed -= onKeyDown;
            GlobalHookManager.Instance.KeyReleased -= onKeyUp;

            CompositionTarget.Rendering -= MainWindow.Instance.UpdateTimer;
            MainWindow.displayTimer.Stop();

            pressedKeys.Clear();

            timer.Stop();

            isRecording = false;
        }

        public void clearRecording()
        {
            if (!isRecording)
            {
                RecordedEvents.Clear();
                pressedKeys.Clear();

                lastMouseX = 0;
                lastMouseY = 0;

                timer.Reset();
            }
        }

        private Recording() { }
    }
}