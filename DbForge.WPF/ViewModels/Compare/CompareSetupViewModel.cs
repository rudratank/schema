using DbForge.Core.Schema;                  // CompareExecutionResult, CompareProgressEvent
using DbForge.WPF.UI.Converters;
using DbForge.WPF.ViewModels.Base;
using DbForge.WPF.Windows.Commands;
using System.Windows.Input;

namespace DbForge.WPF.ViewModels.Compare;

public class CompareSetupViewModel : BaseViewModel
{
    public ConnectionSideViewModel Source { get; }
    public ConnectionSideViewModel Target { get; }

    private readonly SchemaCompareEngine _schemaCompareEngine;
    private CancellationTokenSource? _cts;

    public CompareSetupViewModel (
        ConnectionSideViewModel source,
        ConnectionSideViewModel target,
        SchemaCompareEngine schemaCompareEngine )
    {
        Source = source;
        Target = target;
        _schemaCompareEngine = schemaCompareEngine;

        CompareCommand = new RelayCommand(
            async _ => await CompareAsync(),
            _ => !IsBusy &&
                 Source.HasDatabaseSelected &&
                 Target.HasDatabaseSelected);

        Source.DatabaseSelected += CommandManager.InvalidateRequerySuggested;
        Target.DatabaseSelected += CommandManager.InvalidateRequerySuggested;
    }

    public ICommand CompareCommand { get; }

    // CompareExecutionResult already carries SourceSchema + TargetSchema —
    // the window reads them directly from the result, no need to store them here.
    public event Action<CompareExecutionResult>? CompareCompleted;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            Set(ref _isBusy, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    private async Task CompareAsync ()
    {
        try
        {
            IsBusy = true;

            var sourceProfile = ConnectionProfileFactory.Create(
                Source.Host, Source.Port, Source.Username, Source.Password,
                Source.SelectedProvider, Source.SelectedAuthType, Source.SelectedDatabase);

            var targetProfile = ConnectionProfileFactory.Create(
                Target.Host, Target.Port, Target.Username, Target.Password,
                Target.SelectedProvider, Target.SelectedAuthType, Target.SelectedDatabase);

            _cts = new CancellationTokenSource();

            var progress = new Progress<CompareProgressEvent>(p =>
                StatusMessage = $"{p.Message} ({p.PercentComplete}%)");

            // CompareAsync extracts both schemas internally and returns them
            // in CompareExecutionResult — no separate GetSchemaAsync needed.
            var execution = await _schemaCompareEngine.CompareAsync(
                sourceProfile,
                targetProfile,
                progress,
                _cts.Token);

            CompareCompleted?.Invoke(execution);
        }
        catch ( Exception ex )
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}