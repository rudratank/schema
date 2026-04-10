using DbForge.Core.Models.Schema;
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

    // ── Counters ──────────────────────────────────────────────────────────────
    public int AddedCount { get; set; }
    public int RemovedCount { get; set; }
    public int ModifiedCount { get; set; }

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

    // ── Side-by-side diff pairs (bound to the DataGrid in the SQL pane) ───────
    private ObservableCollection<SqlDiffPair> _diffPairs = new();
    public ObservableCollection<SqlDiffPair> DiffPairs
    {
        get => _diffPairs;
        private set => Set(ref _diffPairs, value);
    }

    // ── Object tree (left panel of the SQL pane) ───────────────────────────────
    private ObservableCollection<DiffTreeNode> _diffTree = new();
    public ObservableCollection<DiffTreeNode> DiffTree
    {
        get => _diffTree;
        private set => Set(ref _diffTree, value);
    }

    // ── SQL pane header labels ─────────────────────────────────────────────────
    public string SourcePaneHeader => _selectedRow != null
        ? $"[{_selectedRow.Owner}].[{_selectedRow.SourceObjectName}]"
        : SourceLabel;

    public string TargetPaneHeader => _selectedRow != null
        ? $"[{_selectedRow.TargetOwner}].[{_selectedRow.TargetObjectName}]"
        : TargetLabel;

    // ── Diff panel update ─────────────────────────────────────────────────────
    private void UpdateDiffPanel ()
    {
        if ( SelectedRow == null )
        {
            DiffPairs = new ObservableCollection<SqlDiffPair>();
            DiffTree = new ObservableCollection<DiffTreeNode>();
            return;
        }

        var tableName = !string.IsNullOrWhiteSpace(SelectedRow.SourceObjectName)
            ? SelectedRow.SourceObjectName
            : SelectedRow.TargetObjectName;

        if ( string.IsNullOrWhiteSpace(tableName) )
        {
            DiffPairs = new ObservableCollection<SqlDiffPair>(new[]
            {
                SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No table name on this row"),
                    SqlDiffLine.Context("-- No table name on this row"))
            });
            DiffTree = new ObservableCollection<DiffTreeNode>();
            return;
        }

        var srcTable = _sourceSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        var tgtTable = _targetSchema?.Tables
            .FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        // Column diffs come from the row itself (populated by the mapper)
        var columnDiffs = SelectedRow.ColumnDiffs;

        var pairs = SqlDiffBuilder.BuildDiffPairs(srcTable, tgtTable, columnDiffs);
        var tree = SqlDiffBuilder.BuildTree(srcTable, tgtTable, columnDiffs, tableName);

        DiffPairs = new ObservableCollection<SqlDiffPair>(pairs);
        DiffTree = tree;
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
            var text = string.Join(Environment.NewLine,
                DiffPairs.Select(p => p.Source.Text));
            if ( !string.IsNullOrEmpty(text) )
                Clipboard.SetText(text);
        });

        CopyTargetCommand = new RelayCommand(_ =>
        {
            var text = string.Join(Environment.NewLine,
                DiffPairs.Select(p => p.Target.Text));
            if ( !string.IsNullOrEmpty(text) )
                Clipboard.SetText(text);
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