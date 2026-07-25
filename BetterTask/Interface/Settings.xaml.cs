using BetterTask.Interface.UserControlPages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BetterTask.Interface
{
    /// <summary>
    /// Interaction logic for settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        private readonly GeneralSettings generalSettings = GeneralSettings.Instance;
        private readonly KeybindsSetting keybindsSetting = new();

        public Settings()
        {
            InitializeComponent();

            ShownContent.Content = generalSettings;
        }

        private void GeneralSettingsClick(object sender, RoutedEventArgs e)
        {
            ShownContent.Content = generalSettings;
        }

        private void KeybindsSettingsClick(object sender, RoutedEventArgs e)
        {
            ShownContent.Content = keybindsSetting;
        }
    }
}
