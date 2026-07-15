using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using System.Diagnostics;

namespace BetterTask.RecordAndPlayBack
{
    public class Recording
    {
        private static readonly Recording instance = new Recording();
        public static Recording Instance => instance;
        private IKeyboardMouseEvents? globalHook;
        public static List<string> RecordedEvents { get; private set; } = new();
        private HashSet<Keys> pressedKeys = new();
        private int lastMouseX;
        private int lastMouseY;
        private const int mouseMoveThreshold = 0;
        private Stopwatch timer = new Stopwatch();



        public void Subscribe()
        {
            if (globalHook != null)
                return;

            RecordedEvents.Clear();

            lastMouseX = Cursor.Position.X;
            lastMouseY = Cursor.Position.Y;

            globalHook = Hook.GlobalEvents();

            globalHook.MouseMove += onMouseMove;
            globalHook.MouseClick += onMouseClick;
            globalHook.MouseWheel += onMouseWheel;
            globalHook.KeyDown += onKeyDown;
            globalHook.KeyUp += onKeyUp;
            timer.Start();


        }

        private void AddEvent(string eventData)
        {
            RecordedEvents.Add($"{timer.ElapsedMilliseconds} {eventData}");
            System.Diagnostics.Debug.WriteLine($"{timer.ElapsedMilliseconds} {eventData}");
        }

        public void onMouseMove(object? sender, MouseEventArgs e)
        {
            int distanceX = Math.Abs(e.X - lastMouseX);
            int distanceY = Math.Abs(e.Y - lastMouseY);

            if (distanceX < mouseMoveThreshold && distanceY < mouseMoveThreshold)
                return;

            lastMouseX = e.X;
            lastMouseY = e.Y;

            AddEvent($"MouseMove: {e.X}, {e.Y}");
        }
        public void onMouseClick(object? sender, MouseEventArgs e)
        {
            AddEvent($"MouseClick: {e.Button}, {e.X}, {e.Y}");
        }
        public void onMouseWheel(object? sender, MouseEventArgs e)
        {
            AddEvent($"MouseWheel: {e.Delta}, {e.X}, {e.Y}");
        }
        public void onKeyDown(object? sender, KeyEventArgs e)
        {
            if (pressedKeys.Contains(e.KeyCode))
                return;
            pressedKeys.Add(e.KeyCode);

            AddEvent($"KeyDown: {e.KeyCode}");
        }
        public void onKeyUp(object? sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.KeyCode);

            AddEvent($"KeyUp: {e.KeyCode}");
        }

        public void Unsubscribe()
        {
            if (globalHook != null)
            {

                globalHook.MouseMove -= onMouseMove;
                globalHook.MouseClick -= onMouseClick;
                globalHook.MouseWheel -= onMouseWheel;

                globalHook.KeyDown -= onKeyDown;
                globalHook.KeyUp -= onKeyUp;

                globalHook.Dispose();
                globalHook = null;
                pressedKeys.Clear();
                timer.Reset();
            }
        }

        public void clearRecording()
        {
            RecordedEvents.Clear();
            lastMouseX = 0;
            lastMouseY = 0;
        }
        private Recording() {}
    }
}