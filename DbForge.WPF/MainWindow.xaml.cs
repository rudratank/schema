using DbForge.Abstractions.Connections;
using DbForge.WPF.ViewModels;
using DbForge.WPF.Views;
using DbForge.WPF.Views.Compare;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.WPF;

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

    private void NewConnection_Click ( object sender, RoutedEventArgs e )
        => OpenConnectionDialog();

    private void OpenConnectionDialog ()
    {
        var dialog = App.Services.GetRequiredService<ConnectionDialog>();
        dialog.Owner = this;
        if ( dialog.ShowDialog() == true && dialog.ViewModel.Result is { } server )
            _vm.AddServer(server);
    }

    private void Exit_Click ( object sender, RoutedEventArgs e )
        => Application.Current.Shutdown();

    private void SchemaCompare_Click ( object sender, RoutedEventArgs e )
        => OpenCompareSetup(preselectedProfile: null);

    private void OpenCompareSetup ( ConnectionProfile? preselectedProfile )
    {
        var setupDialog = App.Services.GetRequiredService<CompareSetupWindow>();
        setupDialog.Owner = this;

        if ( preselectedProfile != null )
            setupDialog.ViewModel.Source.SetFrom(preselectedProfile);

        if ( setupDialog.ShowDialog() != true ) return;
    }
}