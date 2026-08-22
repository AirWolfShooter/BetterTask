using BetterTask.Core;
using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Interface.UserControlPages;
using System.Configuration;
using System.Data;
using System.Windows;

namespace BetterTask
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            GlobalHookManager.Instance.Stop();
            KeybindsManager.Instance.stopListening();
            base.OnExit(e);
        }
    }
}