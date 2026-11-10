using BetterTask.Infrastructure;
using SharpHook;

namespace BetterTask.Core.Input
{
    public sealed class GlobalHookManager
    {
        private static readonly GlobalHookManager instance = new GlobalHookManager();
        public static GlobalHookManager Instance => instance;

        private readonly IGlobalHook hook;
        private bool isStarted;

        private GlobalHookManager()
        {
            hook = new SimpleGlobalHook();
        }

        public event EventHandler<KeyboardHookEventArgs>? KeyPressed;
        public event EventHandler<KeyboardHookEventArgs>? KeyReleased;

        public event EventHandler<MouseHookEventArgs>? MouseMoved;
        public event EventHandler<MouseHookEventArgs>? MousePressed;
        public event EventHandler<MouseHookEventArgs>? MouseReleased;
        public event EventHandler<MouseWheelHookEventArgs>? MouseWheel;
        public event EventHandler<MouseHookEventArgs>? MouseClicked;

        public void Start()
        {
            if (isStarted)
                return;

            isStarted = true;

            hook.KeyPressed += (s, e) => KeyPressed?.Invoke(this, e);
            hook.KeyReleased += (s, e) => KeyReleased?.Invoke(this, e);

            hook.MouseMoved += (s, e) => MouseMoved?.Invoke(this, e);
            hook.MouseClicked += (s, e) => MouseClicked?.Invoke(this, e);
            hook.MousePressed += (s, e) => MousePressed?.Invoke(this, e);
            hook.MouseReleased += (s, e) => MouseReleased?.Invoke(this, e);
            hook.MouseWheel += (s, e) => MouseWheel?.Invoke(this, e);

            try
            {
                Task hookTask = hook.RunAsync();
                _ = hookTask.ContinueWith(
                    task =>
                    {
                        isStarted = false;
                        AppLogger.Error("Input hooks", "The global input hook stopped unexpectedly.", task.Exception);
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
                AppLogger.Info("Input hooks", "Global keyboard and mouse hooks started.");
            }
            catch (Exception ex)
            {
                isStarted = false;
                AppLogger.Error("Input hooks", "Could not start global input hooks.", ex);
                throw;
            }
        }

        public void Stop()
        {
            if (!isStarted)
                return;

            hook.Dispose();
            isStarted = false;
            AppLogger.Info("Input hooks", "Global input hooks stopped.");
        }
    }
}
