using System.Windows;

namespace OPEN_LRD
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LRD_Core? thunderCore = LRD_Core.Instance;

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
            Tools ToolsPage = new Tools();
            ToolsPage.Show();
            return;
        }
    }
    
}