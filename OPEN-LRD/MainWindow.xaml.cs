using System.Windows;
using OPEN_LRD.Visualizer;

namespace OPEN_LRD
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LRD_Core? thunderCore = LRD_Core.Instance;
        private NlpWorkbenchWindow? nlpWorkbenchWindow;
        private VisualizerWindow? visualizerWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Launch_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                thunderCore?.InitializeCore();

                if (thunderCore is not null && thunderCore.initialized)
                {
                    WelcomeBox.Text = "Core Running";
                    LaunchButton.Visibility = Visibility.Hidden;
                    ToolsButton.IsEnabled = true;
                    VisualizerButton.IsEnabled = true;
                    NlpWorkbenchButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Processing Error:  {ex.Message}");
                this.Close();
            }
            return;
        }

        private void Tools_Button_Click(object sender, RoutedEventArgs e)
        {
            if (thunderCore is null || !thunderCore.initialized)
            {
                MessageBox.Show("Launch Core before opening Tools.");
                return;
            }

            Tools ToolsPage = new Tools();
            ToolsPage.Show();
            return;
        }

        private void NlpWorkbench_Button_Click(object sender, RoutedEventArgs e)
        {
            if (thunderCore is null || !thunderCore.initialized)
            {
                MessageBox.Show("Launch Core before opening the NLP Workbench.");
                return;
            }

            if (nlpWorkbenchWindow is not null)
            {
                if (nlpWorkbenchWindow.WindowState == WindowState.Minimized)
                    nlpWorkbenchWindow.WindowState = WindowState.Normal;

                nlpWorkbenchWindow.Activate();
                return;
            }

            nlpWorkbenchWindow = new NlpWorkbenchWindow
            {
                Owner = this
            };
            nlpWorkbenchWindow.Closed += (_, _) => nlpWorkbenchWindow = null;
            nlpWorkbenchWindow.Show();
        }

        private void Visualizer_Button_Click(object sender, RoutedEventArgs e)
        {
            if (thunderCore is null || !thunderCore.initialized)
            {
                MessageBox.Show("Launch Core before opening the Visualizer.");
                return;
            }

            if (visualizerWindow is not null)
            {
                if (visualizerWindow.WindowState == WindowState.Minimized)
                    visualizerWindow.WindowState = WindowState.Maximized;

                visualizerWindow.Activate();
                return;
            }

            visualizerWindow = new VisualizerWindow(thunderCore);
            if (this.IsLoaded)
                visualizerWindow.Owner = this;
            visualizerWindow.Closed += (_, _) => visualizerWindow = null;
            visualizerWindow.Show();
        }
    }
    
}