using DbForge.Abstractions.Connections;
using DbForge.WPF.ViewModels;
using DbForge.WPF.Views;
using DbForge.WPF.Views.Compare;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.WPF
{
    /// <summary>
    /// Main application window with connection management and theme support.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow ( MainViewModel vm )
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        protected override void OnContentRendered ( EventArgs e )
        {
            base.OnContentRendered(e);
            OpenConnectionDialog();
        }

        /// <summary>
        /// Open a new connection dialog.
        /// </summary>
        private void NewConnection_Click ( object sender, RoutedEventArgs e )
            => OpenConnectionDialog();

        private void OpenConnectionDialog ()
        {
            var dialog = App.Services.GetRequiredService<ConnectionDialog>();
            dialog.Owner = this;
            if ( dialog.ShowDialog() == true && dialog.ViewModel.Result is { } server )
                _vm.AddServer(server);
        }

        /// <summary>
        /// Exit the application.
        /// </summary>
        private void Exit_Click ( object sender, RoutedEventArgs e )
            => Application.Current.Shutdown();

        /// <summary>
        /// Open the Schema Compare tool.
        /// </summary>
        private void SchemaCompare_Click ( object sender, RoutedEventArgs e )
            => OpenCompareSetup(preselectedProfile: null);

        private void OpenCompareSetup ( ConnectionProfile? preselectedProfile )
        {
            var setupWindow = App.Services.GetRequiredService<CompareSetupWindow>();
            setupWindow.Owner = this;
            if ( preselectedProfile != null )
                setupWindow.ViewModel.Source.SetFrom(preselectedProfile);
            setupWindow.ShowDialog();
        }

        /// <summary>
        /// Open the Settings window for theme and preferences configuration.
        /// </summary>
        private void Settings_Click ( object sender, RoutedEventArgs e )
        {
            var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
    }
}