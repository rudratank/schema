using DbForge.WPF.ViewModels.Settings;
using System.Windows;

namespace DbForge.WPF.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsWindow ( SettingsViewModel vm )
        {
            InitializeComponent();
            ViewModel = vm;
            DataContext = vm;
        }
    }
}