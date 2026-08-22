using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Core;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Converters;
using BetterTask.Interface.UserControlPages;
using System.Windows.Threading;
using System.Diagnostics;
using BetterTask.Interface.Managers;

namespace BetterTask.Interface
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static MainWindow? instance;
        public static MainWindow Instance => instance!;
        private Recording recorder = Recording.Instance;
        private Playback playback = Playback.Instance;
        private SaveAndLoadEvents saveAndLoad = SaveAndLoadEvents.Instance;
        public static Stopwatch displayTimer = new Stopwatch();
        public static bool loop;

        public MainWindow()
        {
            InitializeComponent();
            instance = this;

            SettingsManager.Instance.loadSettingsFromSaved();
            GeneralSettings.Instance.LoadSettings();
            GlobalHookManager.Instance.Start();
            KeybindsManager.Instance.startListening();
        }

        public void UpdateTimer(object? sender, EventArgs e)
        {
            TimeSpan time = displayTimer.Elapsed;

            Timer.Text = $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        public void resetTimer()
        {
            displayTimer.Restart();
            Timer.Text = "00:00";
        }

        public void recordingClicked(object? sender, RoutedEventArgs? e)
        {
            if (!Recording.isRecording)
            {
                RecordButton.Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#E60001"),
                    (Color)ColorConverter.ConvertFromString("#7A0000"),
                    45);


                RecordButton.Content = "Recording";
                StatusTextBlock.Text = "Recording...";
                recorder.clearRecording();
                recorder.Subscribe();
                
            }
            else
            {
                RecordButton.ClearValue(Button.BackgroundProperty);
                RecordButton.Content = "Record";
                StatusTextBlock.Text = "Recording stopped.";
                recorder.Unsubscribe();
            }
        }
        public async void playbackClicked(object? sender, RoutedEventArgs? e)
        {
            if (Playback.playbackStarted)
            {
                playback.stopPlayback();
                StatusTextBlock.Text = "Playback stopped.";
                PlayBackButton.ClearValue(Button.BackgroundProperty);
                PlayBackButton.Content = "Play";
                ButtonStackPanel.IsEnabled = true;
                return;
            }

            if (Recording.RecordedEvents.Count > 0 && !Recording.isRecording)
            {
                if (sender is Button button)
                {
                    loop = false;
                }

                StatusTextBlock.Text = "Playing Back Recording...";
                PlayBackButton.Content = "Playing";
                ButtonStackPanel.IsEnabled = false;
                PlayBackButton.Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#006fe6"),
                    (Color)ColorConverter.ConvertFromString("#0250a3"),
                    45);

                await playback.StartPlayback(loop);

                StatusTextBlock.Text = "Playback finished.";
                PlayBackButton.ClearValue(Button.BackgroundProperty);
                PlayBackButton.Content = "Play";
                ButtonStackPanel.IsEnabled = true;
            }
        }
        public void saveButtonClicked(object sender, RoutedEventArgs e)
        {
            if (Recording.RecordedEvents.Count > 0 && !Recording.isRecording)
            {
                saveAndLoad.SaveRecording();
            }
        }

        public void loadButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!Recording.isRecording)
            {
                saveAndLoad.LoadRecording();
            }
        }

        public void settingsButtonClicked(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.ShowDialog();
        }
    }
}