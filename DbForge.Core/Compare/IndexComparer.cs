using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares non-PK indexes between two versions of the same table.
    /// Primary key indexes are intentionally excluded here — use PrimaryKeyComparer.
    ///
    /// Checks for:
    ///   • Added / Removed indexes (by name)
    ///   • Modified indexes: uniqueness change, clustered change, column list change,
    ///     column order change, column sort direction change
    /// </summary>
    public class IndexComparer
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            // Exclude PK indexes — handled by PrimaryKeyComparer
            var srcIndexes = source.Indexes
                .Where(i => !i.IsPrimaryKey)
                .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            var tgtIndexes = target.Indexes
                .Where(i => !i.IsPrimaryKey)
                .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            // ── Removed ──────────────────────────────────────────────────────
            foreach ( var (name, srcIdx) in srcIndexes )
            {
                if ( !tgtIndexes.ContainsKey(name) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Index,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Removed,
                        SourceDefinition = srcIdx
                    });
                }
            }

            // ── Added ────────────────────────────────────────────────────────
            foreach ( var (name, tgtIdx) in tgtIndexes )
            {
                if ( !srcIndexes.ContainsKey(name) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Index,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Added,
                        TargetDefinition = tgtIdx
                    });
                }
            }

            // ── Modified ─────────────────────────────────────────────────────
            foreach ( var (name, srcIdx) in srcIndexes )
            {
                if ( !tgtIndexes.TryGetValue(name, out var tgtIdx) )
                    continue;

                var changes = GetChangedProperties(srcIdx, tgtIdx);
                if ( changes.Count > 0 )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Index,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Modified,
                        SourceDefinition = srcIdx,
                        TargetDefinition = tgtIdx,
                        ChangedProperties = changes
                    });
                }
            }

            return diffs;
        }

        private static List<string> GetChangedProperties ( IndexDefinition a, IndexDefinition b )
        {
            var changes = new List<string>();

            if ( a.IsUnique != b.IsUnique )
                changes.Add($"IsUnique: {a.IsUnique} → {b.IsUnique}");

            if ( a.IsClustered != b.IsClustered )
                changes.Add($"IsClustered: {a.IsClustered} → {b.IsClustered}");

            // Compare ordered column list (name + direction)
            var srcCols = a.Columns.OrderBy(c => c.Position).ToList();
            var tgtCols = b.Columns.OrderBy(c => c.Position).ToList();

            var srcColNames = srcCols.Select(c => c.ColumnName).ToList();
            var tgtColNames = tgtCols.Select(c => c.ColumnName).ToList();

            // Added columns
            var added = tgtColNames
                .Except(srcColNames, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if ( added.Count > 0 )
                changes.Add($"ColumnsAdded: {string.Join(", ", added)}");

            // Removed columns
            var removed = srcColNames
                .Except(tgtColNames, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if ( removed.Count > 0 )
                changes.Add($"ColumnsRemoved: {string.Join(", ", removed)}");

            // Order changed (same columns, different sequence)
            if ( added.Count == 0 && removed.Count == 0
                && !srcColNames.SequenceEqual(tgtColNames, StringComparer.OrdinalIgnoreCase) )
                changes.Add($"ColumnOrder changed: [{string.Join(", ", srcColNames)}] → [{string.Join(", ", tgtColNames)}]");

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
    }
}