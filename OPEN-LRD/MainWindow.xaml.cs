using System.Windows;

namespace OPEN_LRD
{
    
    /// <summary>Provides the application launcher and top-level window navigation.</summary>
    public partial class MainWindow : Window
    {
        private LRD_Core? thunderCore = LRD_Core.Instance;

        /// <summary>Creates the application main window.</summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>Initializes the core graph and updates top-level tool availability.</summary>
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

    }
    
}
