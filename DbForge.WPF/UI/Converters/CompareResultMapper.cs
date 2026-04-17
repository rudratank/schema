using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using DbForge.Core.Schema;
using DbForge.WPF.ViewModels.Compare;

namespace DbForge.WPF.UI.Converters
{
    /// <summary>
    /// Converts a <see cref="CompareExecutionResult"/> into a
    /// <see cref="CompareResultViewModel"/> ready for the WPF grid.
    ///
    /// Object types handled: Tables, Procedures, Views, Functions, Triggers, Synonyms.
    ///
    /// Design notes
    /// ─────────────
    /// • Table rows are driven by result.Tables (the summary list) because
    ///   the new SchemaComparer emits ObjectType.Table DiffItems only for
    ///   Added/Removed tables, not for every modified column.
    /// • Column diffs (ObjectType.Column) for a given table are filtered from
    ///   DiffItems by ParentName, which handles both regular and rename diffs.
    /// • Identical tables produce no row (no diff items, status == "Identical").
    /// </summary>
    public static class CompareResultMapper
    {
        public static CompareResultViewModel ToViewModel (
            CompareResult result,
            SchemaModel sourceSchema,
            SchemaModel targetSchema )
        {
            var vm = new CompareResultViewModel
            {
                SourceLabel = sourceSchema.DatabaseName,
                TargetLabel = targetSchema.DatabaseName,
                SourceSchema = sourceSchema,
                TargetSchema = targetSchema,
                StatusText = $"Found {result.DiffItems.Count} differences"
            };


            // Pre-index column diffs by parent table name for O(1) lookups
            var colDiffsByTable = result.DiffItems
                .Where(d => d.ObjectType == ObjectType.Column)
                .GroupBy(d => d.ParentName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Pre-index index + FK diffs by parent table for same reason
            var indexDiffsByTable = result.DiffItems
                .Where(d => d.ObjectType == ObjectType.Index || d.ObjectType == ObjectType.ForeignKey)
                .GroupBy(d => d.ParentName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // ── TABLE rows ────────────────────────────────────────────────────
            foreach ( var tbl in result.Tables )
            {
                if ( tbl.Status == "Identical" )
                    continue;

                var status = MapStatus(tbl.Status);
                var operation = MapOperation(tbl.Status);

                // Gather all sub-object diffs for this table (columns + indexes + FKs)
                var colDiffs = colDiffsByTable.TryGetValue(tbl.Name, out var cd) ? cd : new List<DiffItem>();
                var idxFkDiffs = indexDiffsByTable.TryGetValue(tbl.Name, out var ifd) ? ifd : new List<DiffItem>();
                var allTableDiffs = colDiffs.Concat(idxFkDiffs).ToList();

                vm.AddRow(new CompareRowViewModel
                {
                    ObjectType = "Table",
                    TypeIcon = "⊞",
                    Owner = tbl.Owner,
                    TargetOwner = tbl.Owner,
                    SourceObjectName = tbl.Status == "Added" ? string.Empty : tbl.Name,
                    TargetObjectName = tbl.Status == "Removed" ? string.Empty : tbl.Name,
                    Status = status,
                    Operation = operation,
                    ColumnDiffs = allTableDiffs
                });
            }

            // ── PROCEDURE rows ────────────────────────────────────────────────
            foreach ( var diff in result.DiffItems.Where(d => d.ObjectType == ObjectType.Procedure) )
                vm.AddRow(BuildBodyObjectRow("Procedure", "⚙", diff));

            // ── VIEW rows ─────────────────────────────────────────────────────
            foreach ( var diff in result.DiffItems.Where(d => d.ObjectType == ObjectType.View) )
                vm.AddRow(BuildBodyObjectRow("View", "◫", diff));

            // ── FUNCTION rows ─────────────────────────────────────────────────
            foreach ( var diff in result.DiffItems.Where(d => d.ObjectType == ObjectType.Function) )
                vm.AddRow(BuildBodyObjectRow("Function", "ƒ", diff));

            // ── TRIGGER rows ──────────────────────────────────────────────────
            foreach ( var diff in result.DiffItems.Where(d => d.ObjectType == ObjectType.Trigger) )
            {
                var srcTr = diff.SourceDefinition as TriggerDefinition;
                var tgtTr = diff.TargetDefinition as TriggerDefinition;

                string srcOwner = srcTr?.SchemaName ?? tgtTr?.SchemaName ?? "dbo";
                string tgtOwner = tgtTr?.SchemaName ?? srcTr?.SchemaName ?? "dbo";
                string parentTbl = srcTr?.ParentTable ?? tgtTr?.ParentTable ?? string.Empty;

                // For renames, ObjectName is "OldName → NewName" — extract the right display name
                string srcName = diff.DiffType == DiffType.Added
                    ? string.Empty
                    : srcTr?.Name ?? diff.ObjectName;
                string tgtName = diff.DiffType == DiffType.Removed
                    ? string.Empty
                    : tgtTr?.Name ?? diff.ObjectName;

                vm.AddRow(new CompareRowViewModel
                {
                    ObjectType = "Trigger",
                    TypeIcon = "⚡",
                    Owner = srcOwner,
                    TargetOwner = tgtOwner,
                    ParentName = parentTbl,
                    SourceObjectName = srcName,
                    TargetObjectName = tgtName,
                    Status = MapDiffStatus(diff.DiffType),
                    Operation = MapDiffOperation(diff.DiffType),
                    ColumnDiffs = new List<DiffItem> { diff }
                });
            }

            // ── SYNONYM rows ──────────────────────────────────────────────────
            foreach ( var diff in result.DiffItems.Where(d => d.ObjectType == ObjectType.Synonym) )
                vm.AddRow(BuildBodyObjectRow("Synonym", "↔", diff));

            vm.RefreshCounters();
            return vm;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a row for any "body object" (Procedure / View / Function / Synonym)
        /// where SourceDefinition / TargetDefinition carry Name + SchemaName.
        /// </summary>
        private static CompareRowViewModel BuildBodyObjectRow (
            string objectType, string icon, DiffItem diff )
        {
            GetNameAndSchema(diff.SourceDefinition, out string srcName, out string srcOwner);
            GetNameAndSchema(diff.TargetDefinition, out string tgtName, out string tgtOwner);

            // For renames ObjectName is "OldName → NewName" — fall back gracefully
            if ( string.IsNullOrEmpty(srcName) )
                srcName = diff.DiffType == DiffType.Added ? string.Empty : diff.ObjectName;
            if ( string.IsNullOrEmpty(tgtName) )
                tgtName = diff.DiffType == DiffType.Removed ? string.Empty : diff.ObjectName;
            if ( string.IsNullOrEmpty(srcOwner) ) srcOwner = tgtOwner;
            if ( string.IsNullOrEmpty(tgtOwner) ) tgtOwner = srcOwner;

            return new CompareRowViewModel
            {
                ObjectType = objectType,
                TypeIcon = icon,
                Owner = srcOwner,
                TargetOwner = tgtOwner,
                SourceObjectName = srcName,
                TargetObjectName = tgtName,
                Status = MapDiffStatus(diff.DiffType),
                Operation = MapDiffOperation(diff.DiffType),
                ColumnDiffs = new List<DiffItem> { diff }
            };
        }

        private static void GetNameAndSchema ( object? obj, out string name, out string schema )
        {
            switch ( obj )
            {
                case ProcedureDefinition p: name = p.Name; schema = p.SchemaName; return;
                case ViewDefinition v: name = v.Name; schema = v.SchemaName; return;
                case FunctionDefinition f: name = f.Name; schema = f.SchemaName; return;
                case TriggerDefinition t: name = t.Name; schema = t.SchemaName; return;
                case SynonymDefinition s: name = s.Name; schema = s.SchemaName; return;
                default: name = string.Empty; schema = "dbo"; return;
            }
        }

        private static string MapStatus ( string tableStatus ) => tableStatus switch
        {
            "Added" => "OnlyInTarget",
            "Removed" => "OnlyInSource",
            "Modified" => "Different",
            _ => "Identical"
        };

        private static string MapOperation ( string tableStatus ) => tableStatus switch
        {
            "Added" => "Create",
            "Removed" => "Drop",
            "Modified" => "Update",
            _ => "Equal"
        };

        private static string MapDiffStatus ( DiffType dt ) => dt switch
        {
            DiffType.Added => "OnlyInTarget",
            DiffType.Removed => "OnlyInSource",
            DiffType.Modified => "Different",
            _ => "Identical"
        };

        private static string MapDiffOperation ( DiffType dt ) => dt switch
        {
            DiffType.Added => "Create",
            DiffType.Removed => "Drop",
            DiffType.Modified => "Update",
            _ => "Equal"
        };
    }
}