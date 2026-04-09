using DbForge.Core.Models.Schema;
using DbForge.Providers.SqlServer.Script;
using DbForge.WPF.ViewModels.Base;
using DbForge.WPF.Windows.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DbForge.WPF.ViewModels.Compare;

public class CompareResultViewModel : BaseViewModel
{
    // ── Labels shown in the header bar ───────────────────────────────────────
    public string SourceLabel { get; init; } = string.Empty;
    public string TargetLabel { get; init; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;

    // ── Counters (set by mapper, displayed in status bar) ─────────────────────
    public int AddedCount { get; set; }
    public int RemovedCount { get; set; }
    public int ModifiedCount { get; set; }

    public string TotalSelected =>
        $"{_allRows.Count(r => r.IsChecked)} of {_allRows.Count} objects selected";

    // ── Raw data ──────────────────────────────────────────────────────────────
    private readonly ObservableCollection<CompareRowViewModel> _allRows = new();
    public ListCollectionView ItemsView { get; }

    // ── Schema models — needed so UpdateSqlPanel can generate DDL ─────────────
    // These MUST be set before any row is selected. The mapper sets them via
    // object-initializer immediately after constructing the VM.
    private SchemaModel? _sourceSchema;
    private SchemaModel? _targetSchema;

    public SchemaModel? SourceSchema
    {
        get => _sourceSchema;
        set => Set(ref _sourceSchema, value);
    }

    public SchemaModel? TargetSchema
    {
        get => _targetSchema;
        set => Set(ref _targetSchema, value);
    }

    // ── SQL generator (stateless, safe to reuse) ───────────────────────────────
    private readonly SqlScriptGenerator _sqlGen = new();

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
            UpdateSqlPanel();
        }
    }

    // ── SQL pane content (bound to the two TextBoxes in the XAML) ─────────────
    private string _sourceSql = string.Empty;
    public string SourceSql
    {
        get => _sourceSql;
        set => Set(ref _sourceSql, value);
    }

    private string _targetSql = string.Empty;
    public string TargetSql
    {
        get => _targetSql;
        set => Set(ref _targetSql, value);
    }

    // ── SQL pane header labels ─────────────────────────────────────────────────
    public string SourcePaneHeader => _selectedRow != null
        ? $"[{_selectedRow.Owner}].[{_selectedRow.SourceObjectName}]"
        : SourceLabel;

    public string TargetPaneHeader => _selectedRow != null
        ? $"[{_selectedRow.TargetOwner}].[{_selectedRow.TargetObjectName}]"
        : TargetLabel;

    // ── SQL generation ────────────────────────────────────────────────────────
    private void UpdateSqlPanel ()
    {
        if ( SelectedRow == null )
        {
            SourceSql = string.Empty;
            TargetSql = string.Empty;
            return;
        }

        // Use whichever name is available — OnlyInTarget rows have no source name
        var tableName = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
            ? SelectedRow.SourceObjectName
            : SelectedRow.TargetObjectName;

        if ( string.IsNullOrWhiteSpace(tableName) )
        {
            SourceSql = "-- No table name on this row";
            TargetSql = "-- No table name on this row";
            return;
        }

        var srcTable = _sourceSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        var tgtTable = _targetSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        SourceSql = srcTable != null
            ? _sqlGen.GenerateTableScript(srcTable)
            : "-- Table does not exist in source";

        TargetSql = tgtTable != null
            ? _sqlGen.GenerateTableScript(tgtTable)
            : "-- Table does not exist in target";
    }

    // ── Filters ───────────────────────────────────────────────────────────────
    private bool _showOnlyInSource = true;
    public bool ShowOnlyInSource
    {
        get => _showOnlyInSource;
        set { Set(ref _showOnlyInSource, value); ItemsView.Refresh(); }
    }

    private bool _showDifferent = true;
    public bool ShowDifferent
    {
        get => _showDifferent;
        set { Set(ref _showDifferent, value); ItemsView.Refresh(); }
    }

    private bool _showOnlyInTarget = true;
    public bool ShowOnlyInTarget
    {
        get => _showOnlyInTarget;
        set { Set(ref _showOnlyInTarget, value); ItemsView.Refresh(); }
    }

    private bool _showIdentical = false;
    public bool ShowIdentical
    {
        get => _showIdentical;
        set { Set(ref _showIdentical, value); ItemsView.Refresh(); }
    }

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

    // ── Constructor ───────────────────────────────────────────────────────────
    public CompareResultViewModel ()
    {
        ItemsView = new ListCollectionView(_allRows);

        ItemsView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(CompareRowViewModel.GroupLabel)));

        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(CompareRowViewModel.SortOrder), ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(CompareRowViewModel.SourceObjectName), ListSortDirection.Ascending));

        ItemsView.Filter = obj =>
        {
            if ( obj is not CompareRowViewModel row ) return false;

            bool passesFilter = row.Status switch
            {
                "OnlyInSource" => ShowOnlyInSource,
                "Different" => ShowDifferent,
                "OnlyInTarget" => ShowOnlyInTarget,
                "Identical" => ShowIdentical,
                _ => true
            };

            if ( !passesFilter ) return false;

            if ( !string.IsNullOrWhiteSpace(SearchText) )
            {
                var q = SearchText.Trim();
                return row.SourceObjectName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.TargetObjectName.Contains(q, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        };

        CopySourceCommand = new RelayCommand(_ =>
        {
            if ( !string.IsNullOrEmpty(SourceSql) )
                Clipboard.SetText(SourceSql);
        });

        CopyTargetCommand = new RelayCommand(_ =>
        {
            if ( !string.IsNullOrEmpty(TargetSql) )
                Clipboard.SetText(TargetSql);
        });

        SelectAllCommand = new RelayCommand(_ =>
        {
            foreach ( var r in _allRows )
                r.IsChecked = true;
            OnPropertyChanged(nameof(TotalSelected));
        });
    }

    // ── Population (called by mapper) ─────────────────────────────────────────
    public void AddRow ( CompareRowViewModel row ) => _allRows.Add(row);

    public void RefreshCounters ()
    {
        ItemsView.Refresh();
        OnPropertyChanged(nameof(TotalSelected));
    }
}