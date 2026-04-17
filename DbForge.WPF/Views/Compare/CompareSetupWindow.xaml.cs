using DbForge.Core.Schema;
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

        vm.CompareCompleted += args =>
        {
            // Build the result VM from the completed execution
            var resultVm = CompareResultMapper.ToViewModel(
                args.Execution.Result,
                args.Execution.SourceSchema,
                args.Execution.TargetSchema);

            // Give the result VM everything it needs to self-refresh
            resultVm.SetRefreshContext(
                App.Services.GetRequiredService<SchemaCompareEngine>(),
                args.SourceProfile,
                args.TargetProfile);

            var resultWindow = App.Services.GetRequiredService<CompareResultWindow>();
            resultWindow.DataContext = resultVm;
            resultWindow.Owner = Owner;
            resultWindow.Show();

            DialogResult = true;
            Close();
        };
    }
}