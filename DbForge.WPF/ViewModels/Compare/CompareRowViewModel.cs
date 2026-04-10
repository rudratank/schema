using DbForge.Core.Models.Compare;
using DbForge.WPF.ViewModels.Base;

namespace DbForge.WPF.ViewModels.Compare;

/// <summary>
/// One row in the compare grid. The parent VM generates SQL + column diffs on selection.
/// </summary>
public class CompareRowViewModel : BaseViewModel
{
    public string ObjectType { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = "⊞";
    public string Owner { get; set; } = "dbo";
    public string TargetOwner { get; set; } = "dbo";
    public string SourceObjectName { get; set; } = string.Empty;
    public string TargetObjectName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;

    /// <summary>"OnlyInSource" | "OnlyInTarget" | "Different" | "Identical"</summary>
    public string Status { get; set; } = string.Empty;

    public int SortOrder => Status switch
    {
        "OnlyInSource" => 1,
        "Different" => 2,
        "OnlyInTarget" => 3,
        _ => 4
    };

    public string GroupLabel => Status switch
    {
        "OnlyInSource" => "Only in Source",
        "OnlyInTarget" => "Only in Target",
        "Different" => "Different",
        _ => "Identical"
    };

    /// <summary>"Create" | "Update" | "Drop" | "Equal"</summary>
    public string Operation { get; set; } = string.Empty;

    public string OperationSymbol => Operation switch
    {
        "Create" => "→",
        "Update" => "↔",
        "Drop" => "✕",
        _ => "="
    };

    private bool _isChecked = true;
    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }

    /// <summary>
    /// Column-level diff items for this table row.
    /// Populated by the mapper from CompareResult.DiffItems filtered by ParentName.
    /// The parent VM reads these when building the diff pane.
    /// </summary>
    public List<DiffItem> ColumnDiffs { get; set; } = new();
}