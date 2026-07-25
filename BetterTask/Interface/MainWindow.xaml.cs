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
        private Stopwatch displayTimer = new Stopwatch();

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
                CompositionTarget.Rendering += UpdateTimer;
                displayTimer.Start();
                recorder.Subscribe();
                
            }
            else
            {
                RecordButton.ClearValue(Button.BackgroundProperty);
                CompositionTarget.Rendering -= UpdateTimer;
                displayTimer.Stop();
                RecordButton.Content = "Record";
                StatusTextBlock.Text = "Recording stopped.";
                recorder.Unsubscribe();
            }
        }
        public async void playbackClicked(object? sender, RoutedEventArgs? e)
        {
            if (Recording.RecordedEvents.Count > 0 && !Recording.isRecording)
            {
                StatusTextBlock.Text = "Playing Back Recording...";
                PlayBackButton.Content = "Playing";
                ButtonStackPanel.IsEnabled = false;
                PlayBackButton.Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#006fe6"),
                    (Color)ColorConverter.ConvertFromString("#0250a3"),
                    45);

                CompositionTarget.Rendering += UpdateTimer;
                displayTimer.Restart();

                await playback.StartPlayback();

                StatusTextBlock.Text = "Playback finished.";
                CompositionTarget.Rendering -= UpdateTimer;
                displayTimer.Stop();
                Timer.Text = "00:00";
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