using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    public class IndexComparer //Basic Version
    {
        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var sourceIdx = source.Indexes.ToDictionary(i => i.Name);
            var targetIdx = target.Indexes.ToDictionary(i => i.Name);

            foreach ( var s in sourceIdx )
            {
                if ( !targetIdx.ContainsKey(s.Key) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Index,
                        ObjectName = s.Key,
                        ParentName = source.Name,
                        DiffType = DiffType.Removed
                    });
                }
            }

            foreach ( var t in targetIdx )
            {
                if ( !sourceIdx.ContainsKey(t.Key) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Index,
                        ObjectName = t.Key,
                        ParentName = source.Name,
                        DiffType = DiffType.Added
                    });
                }
            }

            return diffs;
        }
    }
}
