using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;
using DbForge.WPF.ViewModels.Compare;

namespace DbForge.WPF.UI.Converters;

/// <summary>
/// Turns a CompareResult (+ optional schemas) into a CompareResultViewModel.
///
/// KEY FIX: ColumnDiffs is now populated for every row so that UpdateDiffPanel
/// receives the actual diff items when the user selects a row.  Previously it
/// was always empty, which caused rename detection to fall through to
/// "SourceOnly" (red −) instead of "Modified" (amber ~).
/// </summary>
public static class CompareResultMapper
{
    public static CompareResultViewModel ToViewModel (
        CompareResult result,
        SchemaModel? sourceSchema = null,
        SchemaModel? targetSchema = null )
    {
        var vm = new CompareResultViewModel
        {
            SourceLabel = result.SourceDatabase,
            TargetLabel = result.TargetDatabase,
            AddedCount = result.AddedCount,
            RemovedCount = result.RemovedCount,
            ModifiedCount = result.ModifiedCount,
            StatusText = $"Compare finished · {result.DiffItems.Count} difference(s) found",
            SourceSchema = sourceSchema,
            TargetSchema = targetSchema,
        };

        // ── Tables only in source (CREATE in target) ───────────────────────
        foreach ( var td in result.Tables.Where(t => t.Status == "Removed") )
        {
            vm.AddRow(new CompareRowViewModel
            {
                ObjectType = "Table",
                TypeIcon = "⊞",
                Owner = td.Owner ?? "dbo",
                TargetOwner = string.Empty,
                SourceObjectName = td.Name,
                TargetObjectName = string.Empty,
                ParentName = td.Name,
                Status = "OnlyInSource",
                Operation = "Create",
                IsChecked = true,
                // All columns are "removed" diff items for this table
                ColumnDiffs = DiffItemsForTable(result, td.Name),
            });
        }

        // ── Tables only in target (DROP from target) ───────────────────────
        foreach ( var td in result.Tables.Where(t => t.Status == "Added") )
        {
            vm.AddRow(new CompareRowViewModel
            {
                ObjectType = "Table",
                TypeIcon = "⊞",
                Owner = string.Empty,
                TargetOwner = td.Owner ?? "dbo",
                SourceObjectName = string.Empty,
                TargetObjectName = td.Name,
                ParentName = td.Name,
                Status = "OnlyInTarget",
                Operation = "Drop",
                IsChecked = false,
                ColumnDiffs = DiffItemsForTable(result, td.Name),
            });
        }

        // ── Tables present in both ─────────────────────────────────────────
        var tablesWithDiffs = result.DiffItems
            .Where(d => d.ParentName != null)
            .Select(d => d.ParentName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach ( var td in result.Tables.Where(t => t.Status != "Added" && t.Status != "Removed") )
        {
            var colDiffs = DiffItemsForTable(result, td.Name);
            var hasDiffs = tablesWithDiffs.Contains(td.Name);
            var status = hasDiffs ? "Different" : "Identical";
            var operation = hasDiffs ? "Update" : "Equal";

            vm.AddRow(new CompareRowViewModel
            {
                ObjectType = "Table",
                TypeIcon = "⊞",
                Owner = td.Owner ?? "dbo",
                TargetOwner = td.Owner ?? "dbo",
                SourceObjectName = td.Name,
                TargetObjectName = td.Name,
                ParentName = td.Name,
                Status = status,
                Operation = operation,
                IsChecked = hasDiffs,
                ColumnDiffs = colDiffs,   // ← THE FIX: was always new()
            });
        }

        vm.RefreshCounters();
        return vm;
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static List<DiffItem> DiffItemsForTable ( CompareResult result, string tableName ) =>
        result.DiffItems
              .Where(d => d.ParentName != null &&
                     d.ParentName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
              .ToList();
}