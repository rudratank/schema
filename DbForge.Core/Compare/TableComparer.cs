using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Produces a high-level table-level diff summary (Added / Removed / Modified / Identical).
    /// Deep property comparison is intentionally NOT done here — that belongs to
    /// ColumnComparer, IndexComparer, ForeignKeyComparer, and PrimaryKeyComparer.
    /// This comparer only decides whether a table is worth deep-diving into.
    /// </summary>
    public class TableComparer
    {
        public List<TableDiff> Compare (
            List<TableDefinition> source,
            List<TableDefinition> target )
        {
            var result = new List<TableDiff>();

            var sourceMap = source.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            var targetMap = target.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            // Removed tables
            foreach ( var (name, srcTable) in sourceMap )
            {
                if ( !targetMap.ContainsKey(name) )
                {
                    result.Add(new TableDiff
                    {
                        Name = name,
                        Status = "Removed",
                        Owner = srcTable.SchemaName
                    });
                }
            }

            // Added tables
            foreach ( var (name, tgtTable) in targetMap )
            {
                if ( !sourceMap.ContainsKey(name) )
                {
                    result.Add(new TableDiff
                    {
                        Name = name,
                        Status = "Added",
                        Owner = tgtTable.SchemaName
                    });
                }
            }

            // Tables present in both — check for structural changes
            foreach ( var (name, srcTable) in sourceMap )
            {
                if ( !targetMap.TryGetValue(name, out var tgtTable) )
                    continue;

                result.Add(new TableDiff
                {
                    Name = name,
                    Status = IsStructurallyModified(srcTable, tgtTable) ? "Modified" : "Identical",
                    Owner = srcTable.SchemaName
                });
            }

            return result;
        }

        /// <summary>
        /// Quick structural check to label the table as Modified or Identical.
        /// A true "Modified" state means at least one deep comparer will find diffs.
        /// We check column names + count, index names + count, and FK names + count.
        /// This avoids false positives from count-only comparison.
        /// </summary>
        private static bool IsStructurallyModified ( TableDefinition source, TableDefinition target )
        {
            // Column name set diff
            var srcCols = new HashSet<string>(source.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var tgtCols = new HashSet<string>(target.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            if ( !srcCols.SetEquals(tgtCols) )
                return true;

            // Column property diff (type, nullability, default, identity)
            var tgtColMap = target.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            foreach ( var srcCol in source.Columns )
            {
                if ( !tgtColMap.TryGetValue(srcCol.Name, out var tgtCol) )
                    continue; // already caught above

                if ( srcCol.DataType != tgtCol.DataType ||
                    srcCol.FullDataType != tgtCol.FullDataType ||
                    srcCol.IsNullable != tgtCol.IsNullable ||
                    srcCol.DefaultValue != tgtCol.DefaultValue ||
                    srcCol.IsIdentity != tgtCol.IsIdentity ||
                    srcCol.CharacterMaxLength != tgtCol.CharacterMaxLength ||
                    srcCol.NumericPrecision != tgtCol.NumericPrecision ||
                    srcCol.NumericScale != tgtCol.NumericScale )
                    return true;
            }

            // Index name set diff
            var srcIdx = new HashSet<string>(source.Indexes.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
            var tgtIdx = new HashSet<string>(target.Indexes.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
            if ( !srcIdx.SetEquals(tgtIdx) )
                return true;

            // Index property diff (uniqueness, columns)
            var tgtIdxMap = target.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
            foreach ( var srcIndex in source.Indexes )
            {
                if ( !tgtIdxMap.TryGetValue(srcIndex.Name, out var tgtIndex) )
                    continue;

                if ( srcIndex.IsUnique != tgtIndex.IsUnique ||
                    srcIndex.IsClustered != tgtIndex.IsClustered ||
                    !srcIndex.Columns.Select(c => c.ColumnName)
                        .SequenceEqual(tgtIndex.Columns.Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase) )
                    return true;
            }

            // FK name set diff
            var srcFks = new HashSet<string>(source.ForeignKeys.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var tgtFks = new HashSet<string>(target.ForeignKeys.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            if ( !srcFks.SetEquals(tgtFks) )
                return true;

            // FK property diff
            var tgtFkMap = target.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            foreach ( var srcFk in source.ForeignKeys )
            {
                if ( !tgtFkMap.TryGetValue(srcFk.Name, out var tgtFk) )
                    continue;

                if ( !srcFk.Columns.SequenceEqual(tgtFk.Columns, StringComparer.OrdinalIgnoreCase) ||
                    !string.Equals(srcFk.ReferencedTable, tgtFk.ReferencedTable, StringComparison.OrdinalIgnoreCase) ||
                    !srcFk.ReferencedColumns.SequenceEqual(tgtFk.ReferencedColumns, StringComparer.OrdinalIgnoreCase) ||
                    !string.Equals(srcFk.OnDelete, tgtFk.OnDelete, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(srcFk.OnUpdate, tgtFk.OnUpdate, StringComparison.OrdinalIgnoreCase) )
                    return true;
            }

            return false;
        }
    }
}