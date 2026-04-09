using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    public class ColumnComparer
    {
        public List<DiffItem> Compare (
            TableDefinition source,
            TableDefinition target )

        {
            var diffs = new List<DiffItem>();

            var sourceCols = source.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            var targetCols = target.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            var removed = new List<ColumnDefinition>();
            var added = new List<ColumnDefinition>();

            // Removed
            foreach ( var s in sourceCols )
            {
                if ( !targetCols.ContainsKey(s.Key) )
                {
                    removed.Add(s.Value);
                }
            }

            // Added
            foreach ( var t in targetCols )
            {
                if ( !sourceCols.ContainsKey(t.Key) )
                {
                    added.Add(t.Value);
                }
            }

            // 🔥 RENAME DETECTION (IMPORTANT)
            var renamedPairs = new List<(ColumnDefinition oldCol, ColumnDefinition newCol)>();

            foreach ( var r in removed.ToList() )
            {
                foreach ( var a in added.ToList() )
                {
                    if ( IsPossibleRename(r, a) )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Column,
                            ObjectName = r.Name,
                            ParentName = source.Name,
                            DiffType = DiffType.Modified,
                            SourceDefinition = r,
                            TargetDefinition = a,
                            ChangedProperties = new List<string> { "Renamed" }
                        });

                        renamedPairs.Add((r, a));
                        break;
                    }
                }
            }

            // Remove matched rename pairs
            foreach ( var pair in renamedPairs )
            {
                removed.Remove(pair.oldCol);
                added.Remove(pair.newCol);
            }

            // Final Removed
            foreach ( var r in removed )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = r.Name,
                    ParentName = source.Name,
                    DiffType = DiffType.Removed,
                    SourceDefinition = r
                });
            }

            // Final Added
            foreach ( var a in added )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = a.Name,
                    ParentName = source.Name,
                    DiffType = DiffType.Added,
                    TargetDefinition = a
                });
            }

            // Modified (same name)
            foreach ( var s in sourceCols )
            {
                if ( targetCols.TryGetValue(s.Key, out var targetCol) )
                {
                    var changes = GetChangedProperties(s.Value, targetCol);

                    if ( changes.Count > 0 )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Column,
                            ObjectName = s.Key,
                            ParentName = source.Name,
                            DiffType = DiffType.Modified,
                            SourceDefinition = s.Value,
                            TargetDefinition = targetCol,
                            ChangedProperties = changes
                        });
                    }
                }
            }

            return diffs;
        }

        private bool IsPossibleRename ( ColumnDefinition a, ColumnDefinition b )
        {
            return a.DataType == b.DataType &&
                   a.FullDataType == b.FullDataType &&
                   a.IsNullable == b.IsNullable;
        }

        private List<string> GetChangedProperties ( ColumnDefinition a, ColumnDefinition b )
        {
            var changes = new List<string>();

            if ( a.DataType != b.DataType )
                changes.Add("DataType");

            if ( a.FullDataType != b.FullDataType )
                changes.Add("FullDataType");

            if ( a.IsNullable != b.IsNullable )
                changes.Add("IsNullable");

            if ( a.DefaultValue != b.DefaultValue )
                changes.Add("DefaultValue");

            if ( a.IsIdentity != b.IsIdentity )
                changes.Add("Identity");

            if ( a.CharacterMaxLength != b.CharacterMaxLength )
                changes.Add("MaxLength");

            if ( a.NumericPrecision != b.NumericPrecision )
                changes.Add("Precision");

            if ( a.NumericScale != b.NumericScale )
                changes.Add("Scale");

            return changes;
        }
    }
}
