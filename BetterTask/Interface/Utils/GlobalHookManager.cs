using SharpHook;
using SharpHook.Data;

namespace BetterTask.Core
{
    public sealed class GlobalHookManager
    {
        private static readonly Lazy<GlobalHookManager> _instance =
            new(() => new GlobalHookManager());

        public static GlobalHookManager Instance => _instance.Value;

        private readonly IGlobalHook hook;

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
            hook.KeyPressed += (s, e) => KeyPressed?.Invoke(this, e);
            hook.KeyReleased += (s, e) => KeyReleased?.Invoke(this, e);

            hook.MouseMoved += (s, e) => MouseMoved?.Invoke(this, e);
            hook.MouseClicked += (s, e) => MouseClicked?.Invoke(this, e);
            hook.MousePressed += (s, e) => MousePressed?.Invoke(this, e);
            hook.MouseReleased += (s, e) => MouseReleased?.Invoke(this, e);
            hook.MouseWheel += (s, e) => MouseWheel?.Invoke(this, e);

            hook.RunAsync();
        }

        public void Stop()
        {
            hook.Dispose();
        }
    }
}