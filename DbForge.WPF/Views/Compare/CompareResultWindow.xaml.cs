using DbForge.WPF.ViewModels.Compare;
using System.Windows;

namespace DbForge.WPF.Views.Compare;

public partial class CompareResultWindow : Window
{
    public CompareResultWindow ( CompareResultViewModel vm )
    {
        InitializeComponent();
        DataContext = vm;
    }
}