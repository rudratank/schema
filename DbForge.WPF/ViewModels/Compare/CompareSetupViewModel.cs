using DbForge.Core.Schema;
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

    // ── Single, correctly-typed event ────────────────────────────────────────
    // BUG FIX: was declared twice — once as Action<CompareCompletedArgs> and
    // once as Action<CompareExecutionResult>, causing a compile error.
    // BUG FIX: CompareCompletedArgs record is now defined only in
    // CompareResultViewModel (shared across both VMs via same namespace).
    public event Action<CompareCompletedArgs>? CompareCompleted;

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

    // ── Busy state ────────────────────────────────────────────────────────────
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

    // ════════════════════════════════════════════════════════════════════════
    // COMPARE
    // ════════════════════════════════════════════════════════════════════════

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

            // BUG FIX: was Invoke(execution) — wrong type.
            // Now fires the full CompareCompletedArgs so subscribers get the profiles too.
            CompareCompleted?.Invoke(new CompareCompletedArgs(execution, sourceProfile, targetProfile));
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