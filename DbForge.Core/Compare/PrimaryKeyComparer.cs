using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    public class PrimaryKeyComparer
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var sourcePk = source.Indexes.FirstOrDefault(i => i.IsPrimaryKey);
            var targetPk = target.Indexes.FirstOrDefault(i => i.IsPrimaryKey);

            // PK Added
            if ( sourcePk == null && targetPk != null )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = "PRIMARY",
                    ParentName = source.Name,
                    DiffType = DiffType.Added,
                    TargetDefinition = targetPk
                });
                return diffs;
            }

            // PK Removed
            if ( sourcePk != null && targetPk == null )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = "PRIMARY",
                    ParentName = source.Name,
                    DiffType = DiffType.Removed,
                    SourceDefinition = sourcePk
                });
                return diffs;
            }

            if ( sourcePk == null || targetPk == null )
                return diffs;

            var changes = new List<string>();

            var sourceCols = sourcePk.Columns.OrderBy(c => c.Position).ToList();
            var targetCols = targetPk.Columns.OrderBy(c => c.Position).ToList();

            var sourceNames = sourceCols.Select(c => c.ColumnName).ToList();
            var targetNames = targetCols.Select(c => c.ColumnName).ToList();

            // Added columns in PK
            var added = targetNames.Except(sourceNames).ToList();
            if ( added.Any() )
                changes.Add($"ColumnsAdded: {string.Join(",", added)}");

            // Removed columns in PK
            var removed = sourceNames.Except(targetNames).ToList();
            if ( removed.Any() )
                changes.Add($"ColumnsRemoved: {string.Join(",", removed)}");

            // Order change
            if ( !sourceNames.SequenceEqual(targetNames) &&
                !added.Any() && !removed.Any() )
            {
                changes.Add("OrderChanged");
            }

            if ( changes.Any() )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Index,
                    ObjectName = "PRIMARY",
                    ParentName = source.Name,
                    DiffType = DiffType.Modified,
                    SourceDefinition = sourcePk,
                    TargetDefinition = targetPk,
                    ChangedProperties = changes
                });
            }

            return diffs;
        }
    }
}
