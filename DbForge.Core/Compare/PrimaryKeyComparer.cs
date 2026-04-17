using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares the primary key of two versions of the same table.
    /// Detects: Added PK, Removed PK, and Modified PK (column set, order, sort direction, clustered flag).
    ///
    /// ObjectType.Index is used intentionally — a PK is represented as an IndexDefinition
    /// with IsPrimaryKey = true. ObjectName is set to the actual PK constraint name,
    /// falling back to "PRIMARY" for MySQL-style unnamed PKs.
    /// </summary>
    public class PrimaryKeyComparer
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var srcPk = source.Indexes.FirstOrDefault(i => i.IsPrimaryKey);
            var tgtPk = target.Indexes.FirstOrDefault(i => i.IsPrimaryKey);

            // ── PK Added ─────────────────────────────────────────────────────
            if ( srcPk == null && tgtPk != null )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = PkName(tgtPk),
                    ParentName = source.Name,
                    DiffType = DiffType.Added,
                    TargetDefinition = tgtPk
                });
                return diffs;
            }

            // ── PK Removed ───────────────────────────────────────────────────
            if ( srcPk != null && tgtPk == null )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = PkName(srcPk),
                    ParentName = source.Name,
                    DiffType = DiffType.Removed,
                    SourceDefinition = srcPk
                });
                return diffs;
            }

            // Both null → no PK on either side, nothing to compare
            if ( srcPk == null || tgtPk == null )
                return diffs;

            // ── PK Modified ──────────────────────────────────────────────────
            var changes = GetChangedProperties(srcPk, tgtPk);
            if ( changes.Count > 0 )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = PkName(srcPk),
                    ParentName = source.Name,
                    DiffType = DiffType.Modified,
                    SourceDefinition = srcPk,
                    TargetDefinition = tgtPk,
                    ChangedProperties = changes
                });
            }

            return diffs;
        }

        private static List<string> GetChangedProperties ( IndexDefinition src, IndexDefinition tgt )
        {
            var changes = new List<string>();

            // Clustered flag change (CLUSTERED vs NONCLUSTERED)
            if ( src.IsClustered != tgt.IsClustered )
                changes.Add($"IsClustered: {src.IsClustered} → {tgt.IsClustered}");

            var srcCols = src.Columns.OrderBy(c => c.Position).ToList();
            var tgtCols = tgt.Columns.OrderBy(c => c.Position).ToList();

            var srcNames = srcCols.Select(c => c.ColumnName).ToList();
            var tgtNames = tgtCols.Select(c => c.ColumnName).ToList();

            // Columns added to PK
            var added = tgtNames
                .Except(srcNames, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if ( added.Count > 0 )
                changes.Add($"ColumnsAdded: {string.Join(", ", added)}");

            // Columns removed from PK
            var removed = srcNames
                .Except(tgtNames, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if ( removed.Count > 0 )
                changes.Add($"ColumnsRemoved: {string.Join(", ", removed)}");

            // Order changed (same set, different sequence)
            if ( added.Count == 0 && removed.Count == 0
                && !srcNames.SequenceEqual(tgtNames, StringComparer.OrdinalIgnoreCase) )
                changes.Add($"ColumnOrder: [{string.Join(", ", srcNames)}] → [{string.Join(", ", tgtNames)}]");

            // Sort direction changed per column
            var srcDirMap = srcCols.ToDictionary(c => c.ColumnName, c => c.Descending, StringComparer.OrdinalIgnoreCase);
            var tgtDirMap = tgtCols.ToDictionary(c => c.ColumnName, c => c.Descending, StringComparer.OrdinalIgnoreCase);
            foreach ( var (colName, srcDesc) in srcDirMap )
            {
                if ( tgtDirMap.TryGetValue(colName, out var tgtDesc) && srcDesc != tgtDesc )
                    changes.Add($"SortDirection '{colName}': {(srcDesc ? "DESC" : "ASC")} → {(tgtDesc ? "DESC" : "ASC")}");
            }

            return changes;
        }

        private static string PkName ( IndexDefinition pk ) =>
            string.IsNullOrWhiteSpace(pk.Name) ? "PRIMARY" : pk.Name;
    }
}