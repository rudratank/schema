using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    public class ForeignKeyComparer
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var sourceFks = source.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var targetFks = target.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

            // Removed FK
            foreach ( var s in sourceFks )
            {
                if ( !targetFks.ContainsKey(s.Key) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.ForeignKey,
                        ObjectName = s.Key,
                        ParentName = source.Name,
                        DiffType = DiffType.Removed,
                        SourceDefinition = s.Value
                    });
                }
            }

            // Added FK
            foreach ( var t in targetFks )
            {
                if ( !sourceFks.ContainsKey(t.Key) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.ForeignKey,
                        ObjectName = t.Key,
                        ParentName = source.Name,
                        DiffType = DiffType.Added,
                        TargetDefinition = t.Value
                    });
                }
            }

            // Modified FK
            foreach ( var s in sourceFks )
            {
                if ( targetFks.TryGetValue(s.Key, out var targetFk) )
                {
                    var changes = GetChanges(s.Value, targetFk);

                    if ( changes.Count > 0 )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.ForeignKey,
                            ObjectName = s.Key,
                            ParentName = source.Name,
                            DiffType = DiffType.Modified,
                            SourceDefinition = s.Value,
                            TargetDefinition = targetFk,
                            ChangedProperties = changes
                        });
                    }
                }
            }

            return diffs;
        }

        private List<string> GetChanges ( ForeignKeyDefinition a, ForeignKeyDefinition b )
        {
            var changes = new List<string>();

            // Column mapping change
            if ( !a.Columns.SequenceEqual(b.Columns) )
                changes.Add($"Columns: {string.Join(",", a.Columns)} → {string.Join(",", b.Columns)}");

            // Referenced table change
            if ( !string.Equals(a.ReferencedTable, b.ReferencedTable, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"ReferencedTable: {a.ReferencedTable} → {b.ReferencedTable}");

            // Referenced columns change
            if ( !a.ReferencedColumns.SequenceEqual(b.ReferencedColumns) )
                changes.Add($"ReferencedColumns: {string.Join(",", a.ReferencedColumns)} → {string.Join(",", b.ReferencedColumns)}");

            // OnDelete rule
            if ( !string.Equals(a.OnDelete, b.OnDelete, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"OnDelete: {a.OnDelete} → {b.OnDelete}");

            // OnUpdate rule
            if ( !string.Equals(a.OnUpdate, b.OnUpdate, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"OnUpdate: {a.OnUpdate} → {b.OnUpdate}");

            return changes;
        }
    }
}