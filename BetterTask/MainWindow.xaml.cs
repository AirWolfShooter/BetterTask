using BetterTask.RecordAndPlayBack;
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

namespace BetterTask
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Recording recorder = Recording.Instance;
        bool isRecording;
        public MainWindow()
        {
            InitializeComponent();
            
        }
        public void recordingClicked(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                RecordButton.Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#E60001"),
                    (Color)ColorConverter.ConvertFromString("#7A0000"),
                    45);


                RecordButton.Content = "Recording";
                recorder.Subscribe();
                recorder.clearRecording();
                isRecording = true;
            }
            else
            {
                RecordButton.ClearValue(Button.BackgroundProperty);

                isRecording = false;
                RecordButton.Content = "Record";
                recorder.Unsubscribe();
            }
        }
    }
}