using DbForge.WPF.UI.Converters;
using DbForge.WPF.ViewModels.Compare;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.WPF.Views.Compare;

public partial class CompareSetupWindow : Window
{
    public CompareSetupViewModel ViewModel { get; }

    public CompareSetupWindow ( CompareSetupViewModel vm )
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;

        vm.CompareCompleted += result =>
        {
            // result.SourceSchema and result.TargetSchema were extracted
            // inside SchemaCompareEngine.CompareAsync — use them directly.
            var resultVm = CompareResultMapper.ToViewModel(
                result.Result,
                result.SourceSchema,
                result.TargetSchema);

            var window = App.Services.GetRequiredService<CompareResultWindow>();
            window.DataContext = resultVm;
            window.Show();
            Close();
        };
    }
}