using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;
using DbForge.WPF.ViewModels.Compare;

namespace DbForge.WPF.UI.Converters;

/// <summary>
/// Turns a CompareResult (+ optional schemas) into a CompareResultViewModel.
/// Each table becomes one flat row. The schemas are stored on the VM so the
/// SQL panel can generate DDL when the user selects a row.
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
            // Store schemas so UpdateSqlPanel can do table lookups on selection
            SourceSchema = sourceSchema,
            TargetSchema = targetSchema,
        };

        // ── Tables only in source (will be CREATEd in target) ────────────────
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
            });
        }

        // ── Tables only in target (will be DROPped from target) ───────────────
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
            });
        }

        // ── Tables present in both ────────────────────────────────────────────
        var tablesWithDiffs = result.DiffItems
            .Where(d => d.ParentName != null)
            .Select(d => d.ParentName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach ( var td in result.Tables.Where(t => t.Status != "Added" && t.Status != "Removed") )
        {
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
            });
        }

        vm.RefreshCounters();
        return vm;
    }
}