using DbForge.Abstractions.Connections;
using DbForge.Core.Models.Schema;
using DbForge.Core.Schema;
using DbForge.WPF.UI.Converters;
using DbForge.WPF.ViewModels.Base;
using DbForge.WPF.Windows.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DbForge.WPF.ViewModels.Compare;

// ── Shared args record (single definition — used by both Setup and Result VMs) ─
// BUG FIX: was duplicated inside both CompareSetupViewModel and CompareResultViewModel,
// causing a conflict.  Moved to namespace level so both classes share one type.
public record CompareCompletedArgs (
    CompareExecutionResult Execution,
    ConnectionProfile SourceProfile,
    ConnectionProfile TargetProfile );

public class CompareResultViewModel : BaseViewModel
{
    // ── Refresh engine (set via SetRefreshContext after construction) ─────────
    private SchemaCompareEngine? _engine;
    private ConnectionProfile? _sourceProfile;
    private ConnectionProfile? _targetProfile;

    /// <summary>
    /// Fires after every successful refresh so external observers (e.g. a parent
    /// window updating its title) can react to the new result.
    /// </summary>
    public event Action<CompareCompletedArgs>? CompareCompleted;

    /// <summary>
    /// Call this after construction so the Refresh button can re-run the compare.
    /// </summary>
    public void SetRefreshContext (
        SchemaCompareEngine engine,
        ConnectionProfile sourceProfile,
        ConnectionProfile targetProfile )
    {
        _engine = engine;
        _sourceProfile = sourceProfile;
        _targetProfile = targetProfile;
    }

    // ── Busy state ────────────────────────────────────────────────────────────
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            OnPropertyChanged(nameof(IsNotBusy));
            Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
        }
    }
    public bool IsNotBusy => !_isBusy;

    // ── Labels ────────────────────────────────────────────────────────────────
    public string SourceLabel { get; init; } = string.Empty;
    public string TargetLabel { get; init; } = string.Empty;

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    // ── Counters ──────────────────────────────────────────────────────────────
    private int _addedCount;
    private int _removedCount;
    private int _modifiedCount;

    public int AddedCount { get => _addedCount; private set => Set(ref _addedCount, value); }
    public int RemovedCount { get => _removedCount; private set => Set(ref _removedCount, value); }
    public int ModifiedCount { get => _modifiedCount; private set => Set(ref _modifiedCount, value); }

    public string TotalSelected =>
        $"{_allRows.Count(r => r.IsChecked)} of {_allRows.Count} objects selected";

    // ── Raw data ──────────────────────────────────────────────────────────────
    private readonly ObservableCollection<CompareRowViewModel> _allRows = new();
    public ListCollectionView ItemsView { get; }

    // ── Schema models ─────────────────────────────────────────────────────────
    private SchemaModel? _sourceSchema;
    public SchemaModel? SourceSchema
    {
        get => _sourceSchema;
        set => Set(ref _sourceSchema, value);
    }

    private SchemaModel? _targetSchema;
    public SchemaModel? TargetSchema
    {
        get => _targetSchema;
        set => Set(ref _targetSchema, value);
    }

    // ── Selected row ──────────────────────────────────────────────────────────
    private CompareRowViewModel? _selectedRow;
    public CompareRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            Set(ref _selectedRow, value);
            OnPropertyChanged(nameof(SourcePaneHeader));
            OnPropertyChanged(nameof(TargetPaneHeader));
            UpdateDiffPanel();
        }
    }

    // ── Side-by-side diff pairs ───────────────────────────────────────────────
    private ObservableCollection<SqlDiffPair> _diffPairs = new();
    public ObservableCollection<SqlDiffPair> DiffPairs
    {
        get => _diffPairs;
        private set => Set(ref _diffPairs, value);
    }

    // ── Object tree ───────────────────────────────────────────────────────────
    private ObservableCollection<DiffTreeNode> _diffTree = new();
    public ObservableCollection<DiffTreeNode> DiffTree
    {
        get => _diffTree;
        private set => Set(ref _diffTree, value);
    }

    // ── SQL pane headers ──────────────────────────────────────────────────────
    public string SourcePaneHeader
    {
        get
        {
            if ( _selectedRow == null ) return SourceLabel;
            var owner = _selectedRow.Owner;
            var name = _selectedRow.SourceObjectName;
            return string.IsNullOrEmpty(name) ? $"[{owner}]" : $"[{owner}].[{name}]";
        }
    }

    public string TargetPaneHeader
    {
        get
        {
            if ( _selectedRow == null ) return TargetLabel;
            var owner = _selectedRow.TargetOwner;
            var name = _selectedRow.TargetObjectName;
            return string.IsNullOrEmpty(name) ? $"[{owner}]" : $"[{owner}].[{name}]";
        }
    }

    // ── Status filters ────────────────────────────────────────────────────────
    private bool _showOnlyInSource = true;
    public bool ShowOnlyInSource { get => _showOnlyInSource; set { Set(ref _showOnlyInSource, value); ItemsView.Refresh(); } }

    private bool _showDifferent = true;
    public bool ShowDifferent { get => _showDifferent; set { Set(ref _showDifferent, value); ItemsView.Refresh(); } }

    private bool _showOnlyInTarget = true;
    public bool ShowOnlyInTarget { get => _showOnlyInTarget; set { Set(ref _showOnlyInTarget, value); ItemsView.Refresh(); } }

    private bool _showIdentical = false;
    public bool ShowIdentical { get => _showIdentical; set { Set(ref _showIdentical, value); ItemsView.Refresh(); } }

    // ── Object type filters ───────────────────────────────────────────────────
    private bool _showTables = true;
    private bool _showProcedures = true;
    private bool _showViews = true;
    private bool _showFunctions = true;
    private bool _showTriggers = true;
    private bool _showSynonyms = true;

    public bool ShowTables { get => _showTables; set { Set(ref _showTables, value); ItemsView.Refresh(); } }
    public bool ShowProcedures { get => _showProcedures; set { Set(ref _showProcedures, value); ItemsView.Refresh(); } }
    public bool ShowViews { get => _showViews; set { Set(ref _showViews, value); ItemsView.Refresh(); } }
    public bool ShowFunctions { get => _showFunctions; set { Set(ref _showFunctions, value); ItemsView.Refresh(); } }
    public bool ShowTriggers { get => _showTriggers; set { Set(ref _showTriggers, value); ItemsView.Refresh(); } }
    public bool ShowSynonyms { get => _showSynonyms; set { Set(ref _showSynonyms, value); ItemsView.Refresh(); } }

    // ── Search ────────────────────────────────────────────────────────────────
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { Set(ref _searchText, value); ItemsView.Refresh(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand CopySourceCommand { get; }
    public ICommand CopyTargetCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand RefreshCommand { get; }

    // ════════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ════════════════════════════════════════════════════════════════════════

    public CompareResultViewModel ()
    {
        ItemsView = new ListCollectionView(_allRows);

        ItemsView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(CompareRowViewModel.GroupLabel)));

        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(CompareRowViewModel.SortOrder), ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(CompareRowViewModel.ObjectType), ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(CompareRowViewModel.SourceObjectName), ListSortDirection.Ascending));

        ItemsView.Filter = obj =>
        {
            if ( obj is not CompareRowViewModel row ) return false;

            bool passesStatus = row.Status switch
            {
                "OnlyInSource" => ShowOnlyInSource,
                "Different" => ShowDifferent,
                "OnlyInTarget" => ShowOnlyInTarget,
                "Identical" => ShowIdentical,
                _ => true
            };
            if ( !passesStatus ) return false;

            bool passesType = row.ObjectType switch
            {
                "Table" => ShowTables,
                "Procedure" => ShowProcedures,
                "View" => ShowViews,
                "Function" => ShowFunctions,
                "Trigger" => ShowTriggers,
                "Synonym" => ShowSynonyms,
                _ => true
            };
            if ( !passesType ) return false;

            if ( !string.IsNullOrWhiteSpace(SearchText) )
            {
                var q = SearchText.Trim();
                return row.SourceObjectName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.TargetObjectName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.ParentName.Contains(q, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        };

        CopySourceCommand = new RelayCommand(_ =>
        {
            var text = string.Join(Environment.NewLine,
                DiffPairs.Select(p => p.Source.Text).Where(t => !string.IsNullOrEmpty(t)));
            if ( !string.IsNullOrEmpty(text) ) Clipboard.SetText(text);
        });

        CopyTargetCommand = new RelayCommand(_ =>
        {
            var text = string.Join(Environment.NewLine,
                DiffPairs.Select(p => p.Target.Text).Where(t => !string.IsNullOrEmpty(t)));
            if ( !string.IsNullOrEmpty(text) ) Clipboard.SetText(text);
        });

        SelectAllCommand = new RelayCommand(_ =>
        {
            foreach ( var r in _allRows ) r.IsChecked = true;
            OnPropertyChanged(nameof(TotalSelected));
        });

        DeselectAllCommand = new RelayCommand(_ =>
        {
            foreach ( var r in _allRows ) r.IsChecked = false;
            OnPropertyChanged(nameof(TotalSelected));
        });

        RefreshCommand = new RelayCommand(
            async _ => await RefreshAsync(),
            _ => IsNotBusy && _engine != null && _sourceProfile != null && _targetProfile != null);
    }

    // ════════════════════════════════════════════════════════════════════════
    // REFRESH
    // ════════════════════════════════════════════════════════════════════════

    private async Task RefreshAsync ()
    {
        if ( _engine == null || _sourceProfile == null || _targetProfile == null ) return;

        try
        {
            IsBusy = true;
            StatusText = "Refreshing…";

            var progress = new Progress<CompareProgressEvent>(p =>
                Application.Current.Dispatcher.Invoke(() =>
                    StatusText = $"{p.Message} ({p.PercentComplete}%)"));

            var execution = await _engine.CompareAsync(
                _sourceProfile, _targetProfile, progress);

            // BUG FIX: was `_lastSourceProfile = sourceProfile` — 'sourceProfile' is
            // not a local variable here; the fields _sourceProfile/_targetProfile already
            // hold the correct profiles.  Fire the event with what we already have.
            CompareCompleted?.Invoke(
                new CompareCompletedArgs(execution, _sourceProfile, _targetProfile));

            // Rebuild on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _allRows.Clear();
                SelectedRow = null;
                DiffPairs = new ObservableCollection<SqlDiffPair>();
                DiffTree = new ObservableCollection<DiffTreeNode>();

                var fresh = CompareResultMapper.ToViewModel(
                    execution.Result, execution.SourceSchema, execution.TargetSchema);

                SourceSchema = fresh.SourceSchema;
                TargetSchema = fresh.TargetSchema;

                foreach ( var row in fresh.GetAllRows() )
                    _allRows.Add(row);

                RefreshCounters();
                StatusText = $"Refreshed — {_allRows.Count} differences";
            });
        }
        catch ( Exception ex )
        {
            Application.Current.Dispatcher.Invoke(() =>
                StatusText = $"Refresh failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // POPULATION
    // ════════════════════════════════════════════════════════════════════════

    public void AddRow ( CompareRowViewModel row ) => _allRows.Add(row);

    /// <summary>Exposes rows so Refresh can copy them from a freshly-mapped VM.</summary>
    public IEnumerable<CompareRowViewModel> GetAllRows () => _allRows;

    public void RefreshCounters ()
    {
        AddedCount = _allRows.Count(r => r.Status == "OnlyInTarget");
        RemovedCount = _allRows.Count(r => r.Status == "OnlyInSource");
        ModifiedCount = _allRows.Count(r => r.Status == "Different");
        ItemsView.Refresh();
        OnPropertyChanged(nameof(TotalSelected));
    }

    // ════════════════════════════════════════════════════════════════════════
    // DIFF PANEL ROUTING
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateDiffPanel ()
    {
        if ( SelectedRow == null )
        {
            DiffPairs = new ObservableCollection<SqlDiffPair>();
            DiffTree = new ObservableCollection<DiffTreeNode>();
            return;
        }

        switch ( SelectedRow.ObjectType )
        {
            case "Table": UpdateTableDiffPanel(); break;
            case "Procedure": UpdateProcedureDiffPanel(); break;
            case "View": UpdateViewDiffPanel(); break;
            case "Function": UpdateFunctionDiffPanel(); break;
            case "Trigger": UpdateTriggerDiffPanel(); break;
            case "Synonym": UpdateSynonymDiffPanel(); break;
            default:
                DiffPairs = new ObservableCollection<SqlDiffPair>();
                DiffTree = new ObservableCollection<DiffTreeNode>();
                break;
        }
    }

    private void UpdateTableDiffPanel ()
    {
        var tableName = !string.IsNullOrWhiteSpace(SelectedRow!.SourceObjectName)
            ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;

        if ( string.IsNullOrWhiteSpace(tableName) )
        {
            DiffPairs = new ObservableCollection<SqlDiffPair>(new[]
            {
                SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No table name available"),
                    SqlDiffLine.Context("-- No table name available"))
            });
            DiffTree = new ObservableCollection<DiffTreeNode>();
            return;
        }

        var srcTable = _sourceSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        var tgtTable = _targetSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        var columnDiffs = SelectedRow.ColumnDiffs;

        DiffPairs = new ObservableCollection<SqlDiffPair>(
            SqlDiffBuilder.BuildDiffPairs(srcTable, tgtTable, columnDiffs));
        DiffTree = SqlDiffBuilder.BuildTree(srcTable, tgtTable, columnDiffs, tableName);
    }

    private void UpdateProcedureDiffPanel ()
    {
        var diff = SelectedRow!.ColumnDiffs.FirstOrDefault();
        var srcProc = diff?.SourceDefinition as ProcedureDefinition;
        var tgtProc = diff?.TargetDefinition as ProcedureDefinition;
        var name = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
                        ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;
        DiffPairs = new ObservableCollection<SqlDiffPair>(ProcedureDiffBuilder.BuildDiffPairs(srcProc, tgtProc));
        DiffTree = ProcedureDiffBuilder.BuildTree(srcProc, tgtProc, name);
    }

    private void UpdateViewDiffPanel ()
    {
        var diff = SelectedRow!.ColumnDiffs.FirstOrDefault();
        var srcView = diff?.SourceDefinition as ViewDefinition;
        var tgtView = diff?.TargetDefinition as ViewDefinition;
        var name = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
                        ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;
        DiffPairs = new ObservableCollection<SqlDiffPair>(ViewDiffBuilder.BuildDiffPairs(srcView, tgtView));
        DiffTree = ViewDiffBuilder.BuildTree(srcView, tgtView, name);
    }

    private void UpdateFunctionDiffPanel ()
    {
        var diff = SelectedRow!.ColumnDiffs.FirstOrDefault();
        var srcFn = diff?.SourceDefinition as FunctionDefinition;
        var tgtFn = diff?.TargetDefinition as FunctionDefinition;
        var name = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
                      ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;
        DiffPairs = new ObservableCollection<SqlDiffPair>(FunctionDiffBuilder.BuildDiffPairs(srcFn, tgtFn));
        DiffTree = FunctionDiffBuilder.BuildTree(srcFn, tgtFn, name);
    }

    private void UpdateTriggerDiffPanel ()
    {
        var diff = SelectedRow!.ColumnDiffs.FirstOrDefault();
        var srcTr = diff?.SourceDefinition as TriggerDefinition;
        var tgtTr = diff?.TargetDefinition as TriggerDefinition;
        var name = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
                      ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;
        DiffPairs = new ObservableCollection<SqlDiffPair>(TriggerDiffBuilder.BuildDiffPairs(srcTr, tgtTr));
        DiffTree = TriggerDiffBuilder.BuildTree(srcTr, tgtTr, name);
    }

    private void UpdateSynonymDiffPanel ()
    {
        var diff = SelectedRow!.ColumnDiffs.FirstOrDefault();
        var srcSyn = diff?.SourceDefinition as SynonymDefinition;
        var tgtSyn = diff?.TargetDefinition as SynonymDefinition;
        var name = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
                       ? SelectedRow.SourceObjectName : SelectedRow.TargetObjectName;
        DiffPairs = new ObservableCollection<SqlDiffPair>(SynonymDiffBuilder.BuildDiffPairs(srcSyn, tgtSyn));
        DiffTree = SynonymDiffBuilder.BuildTree(srcSyn, tgtSyn, name);
    }
}