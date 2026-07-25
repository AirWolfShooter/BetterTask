using BetterTask.Core;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static BetterTask.Interface.UserControlPages.KeybindsSetting;

namespace BetterTask.Interface.UserControlPages
{
    /// <summary>
    /// Interaction logic for GeneralSettings.xaml
    /// </summary>
    public partial class GeneralSettings : UserControl
    {
        private static GeneralSettings instance = new GeneralSettings();
        public static GeneralSettings Instance => instance;
        private static HttpClient client = new HttpClient();
        private const string CurrentVersion = "1.0.0";
        private string repoOwner = "AirWolfShooter";
        private const string repoName = "BetterTask";

        private GeneralSettings()
        {
            InitializeComponent();
            LoadSettings();
        }

        public void LoadSettings()
        {
            stayOnTop.IsChecked = SettingsManager.Instance.settings.StayOnTop ?? false;
            checkForUpdates.IsChecked = SettingsManager.Instance.settings.CheckForUpdates ?? false;
            saveAutomatically.IsChecked = SettingsManager.Instance.settings.SaveAutomatically ?? false;

            checkSettings();
        }

        

        public void stayOnTopClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.settings.StayOnTop = stayOnTop.IsChecked;
            checkSettings();
        }

        public void checkForUpdatesClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.settings.CheckForUpdates = checkForUpdates.IsChecked;
            checkSettings();
        }

        public void saveAutoClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.settings.SaveAutomatically = saveAutomatically.IsChecked;
            checkSettings();
        }

        public void saveSettingsClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.saveSettings();
        }

        public void exportSettingsClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.exportSettings();
        }

        public void loadSettingsClicked(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.loadSettings();
            LoadSettings();
        }

        public void checkSettings()
        {
            keepOnTop();

            if (SettingsManager.Instance.settings.SaveAutomatically ?? false)
            {
                SettingsManager.Instance.saveSettings();

            }
        }

        public void keepOnTop()
        {
            {
                MainWindow.Instance.Topmost = SettingsManager.Instance.settings.StayOnTop ?? false;
            }
            
        }

        public async Task checkForUpdatesAsync()
        {
            // Ill work on this later
        }
    }
}