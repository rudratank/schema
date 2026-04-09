using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    public class TableComparer
    {
        public List<TableDiff> Compare (
            List<TableDefinition> source,
            List<TableDefinition> target )
        {
            var result = new List<TableDiff>();

            var sourceMap = source.ToDictionary(t => t.Name);
            var targetMap = target.ToDictionary(t => t.Name);

            // Removed in target
            foreach ( var s in sourceMap )
            {
                if ( !targetMap.ContainsKey(s.Key) )
                {
                    result.Add(new TableDiff
                    {
                        Name = s.Key,
                        Status = "Removed"
                    });
                }
            }

            // Added in target
            foreach ( var t in targetMap )
            {
                if ( !sourceMap.ContainsKey(t.Key) )
                {
                    result.Add(new TableDiff
                    {
                        Name = t.Key,
                        Status = "Added"
                    });
                }
            }

            // Modified
            foreach ( var s in sourceMap )
            {
                if ( targetMap.TryGetValue(s.Key, out var targetTable) )
                {
                    if ( IsModified(s.Value, targetTable) )
                    {
                        result.Add(new TableDiff
                        {
                            Name = s.Key,
                            Status = "Modified"
                        });
                    }
                }
            }

            return result;
        }

        private bool IsModified ( TableDefinition source, TableDefinition target )
        {
            if ( source.Columns.Count != target.Columns.Count )
                return true;

            if ( source.Indexes.Count != target.Indexes.Count )
                return true;

            if ( source.ForeignKeys.Count != target.ForeignKeys.Count )
                return true;

            return false;
        }
    }
}
