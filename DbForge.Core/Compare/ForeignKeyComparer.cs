using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares foreign keys between two versions of the same table.
    ///
    /// Checks for:
    ///   • Added / Removed FKs (by constraint name)
    ///   • Modified FKs: column mapping, referenced table, referenced columns,
    ///     ON DELETE rule, ON UPDATE rule
    ///
    /// Note: FK names are the primary key. If the same logical relationship exists
    /// under a different constraint name, it will appear as Removed + Added, not
    /// Modified. Rename detection for FKs is intentionally omitted — constraint
    /// names are not semantic and renaming tools handle that separately.
    /// </summary>
    public class ForeignKeyComparer
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var srcFks = source.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var tgtFks = target.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

            // ── Removed ──────────────────────────────────────────────────────
            foreach ( var (name, srcFk) in srcFks )
            {
                if ( !tgtFks.ContainsKey(name) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.ForeignKey,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Removed,
                        SourceDefinition = srcFk
                    });
                }
            }

            // ── Added ────────────────────────────────────────────────────────
            foreach ( var (name, tgtFk) in tgtFks )
            {
                if ( !srcFks.ContainsKey(name) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.ForeignKey,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Added,
                        TargetDefinition = tgtFk
                    });
                }
            }

            // ── Modified ─────────────────────────────────────────────────────
            foreach ( var (name, srcFk) in srcFks )
            {
                if ( !tgtFks.TryGetValue(name, out var tgtFk) )
                    continue;

                var changes = GetChangedProperties(srcFk, tgtFk);
                if ( changes.Count > 0 )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.ForeignKey,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Modified,
                        SourceDefinition = srcFk,
                        TargetDefinition = tgtFk,
                        ChangedProperties = changes
                    });
                }
            }

            return diffs;
        }

        private static List<string> GetChangedProperties ( ForeignKeyDefinition a, ForeignKeyDefinition b )
        {
            var changes = new List<string>();

            // Column mapping — order matters for composite FKs
            if ( !a.Columns.SequenceEqual(b.Columns, StringComparer.OrdinalIgnoreCase) )
                changes.Add($"Columns: [{string.Join(", ", a.Columns)}] → [{string.Join(", ", b.Columns)}]");

            // Referenced table
            if ( !string.Equals(a.ReferencedTable, b.ReferencedTable, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"ReferencedTable: {a.ReferencedTable} → {b.ReferencedTable}");

            // Referenced columns — order matters
            if ( !a.ReferencedColumns.SequenceEqual(b.ReferencedColumns, StringComparer.OrdinalIgnoreCase) )
                changes.Add($"ReferencedColumns: [{string.Join(", ", a.ReferencedColumns)}] → [{string.Join(", ", b.ReferencedColumns)}]");

            // Referential action rules
            if ( !string.Equals(a.OnDelete, b.OnDelete, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"OnDelete: {a.OnDelete} → {b.OnDelete}");

            if ( !string.Equals(a.OnUpdate, b.OnUpdate, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"OnUpdate: {a.OnUpdate} → {b.OnUpdate}");

            return changes;
        }
    }
}