using DbForge.WPF.ViewModels;
using System.Windows;

namespace DbForge.WPF.Views;

public partial class ConnectionDialog : Window
{
    private readonly ConnectionDialogViewModel _vm;

    public ConnectionDialog ( ConnectionDialogViewModel vm )
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // When the ViewModel says "I'm done", close this window
        vm.RequestClose += connected =>
        {
            DialogResult = connected;
            Close();
        };
    }

    public ConnectionDialogViewModel ViewModel => _vm;
}