using DbForge.Core.Models.Compare;
using DbForge.WPF.ViewModels.Base;

namespace DbForge.WPF.ViewModels.Compare;

/// <summary>
/// One row in the compare result grid.
/// The parent <see cref="CompareResultViewModel"/> populates DiffPairs and
/// DiffTree when this row is selected.
/// </summary>
public class CompareRowViewModel : BaseViewModel
{
    /// <summary>"Table" | "Procedure" | "View" | "Function" | "Trigger" | "Synonym"</summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>Unicode glyph shown next to the object name.</summary>
    public string TypeIcon { get; set; } = "⊞";

    /// <summary>Schema name of the source-side object (e.g. "dbo").</summary>
    public string Owner { get; set; } = "dbo";

    /// <summary>Schema name of the target-side object.</summary>
    public string TargetOwner { get; set; } = "dbo";

    /// <summary>Object name in the source. Empty when the object only exists in target.</summary>
    public string SourceObjectName { get; set; } = string.Empty;

    /// <summary>Object name in the target. Empty when the object only exists in source.</summary>
    public string TargetObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Parent context — for triggers: the parent table name.
    /// Empty for top-level objects.
    /// </summary>
    public string ParentName { get; set; } = string.Empty;

    /// <summary>"OnlyInSource" | "OnlyInTarget" | "Different" | "Identical"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Controls sort position within the grouped view.</summary>
    public int SortOrder => Status switch
    {
        "OnlyInSource" => 1,
        "Different" => 2,
        "OnlyInTarget" => 3,
        _ => 4
    };

    /// <summary>Group header label shown by the CollectionView GroupDescription.</summary>
    public string GroupLabel => Status switch
    {
        "OnlyInSource" => "Only in Source",
        "OnlyInTarget" => "Only in Target",
        "Different" => "Different",
        _ => "Identical"
    };

    /// <summary>"Create" | "Update" | "Drop" | "Equal"</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Short symbol shown in the Operation column.</summary>
    public string OperationSymbol => Operation switch
    {
        "Create" => "+",
        "Update" => "~",
        "Drop" => "−",
        _ => "="
    };

    private bool _isChecked = true;
    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }

    /// <summary>
    /// Column/index/FK-level DiffItems for this object.
    /// For tables: all column, index, and FK diffs.
    /// For body objects: a single-element list containing the object's DiffItem.
    /// Read by the parent VM when building the diff pane.
    /// </summary>
    public List<DiffItem> ColumnDiffs { get; set; } = new();
}