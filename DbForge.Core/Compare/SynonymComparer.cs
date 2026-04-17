using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares synonyms between two schemas.
    ///
    /// A synonym maps a local name to a base object name — there is no body.
    /// Comparison is purely structural:
    ///   • Added / Removed (name not found in the other schema)
    ///   • Modified (same name, different BaseObjectName)
    ///
    /// Rename detection is intentionally omitted. A different name pointing to
    /// the same base object is Added + Removed, not a rename — there is no
    /// meaningful body signal to drive reliable rename scoring for synonyms.
    /// </summary>
    public class SynonymComparer
    {
        public List<DiffItem> Compare ( List<SynonymDefinition> source, List<SynonymDefinition> target )
        {
            var diffs = new List<DiffItem>();

            static string Key ( SynonymDefinition s ) => $"{s.SchemaName}.{s.Name}";
            var srcMap = source.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            // ── Modified & Removed ───────────────────────────────────────────
            foreach ( var (key, src) in srcMap )
            {
                if ( tgtMap.TryGetValue(key, out var tgt) )
                {
                    if ( !string.Equals(
                            NormalizeObjectName(src.BaseObjectName),
                            NormalizeObjectName(tgt.BaseObjectName),
                            StringComparison.OrdinalIgnoreCase) )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Synonym,
                            ObjectName = src.Name,
                            ParentName = src.SchemaName,
                            DiffType = DiffType.Modified,
                            SourceDefinition = src,
                            TargetDefinition = tgt,
                            ChangedProperties = new List<string>
                            {
                                $"Base object: {src.BaseObjectName} → {tgt.BaseObjectName}"
                            }
                        });
                    }
                    // identical — skip
                }
                else
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Synonym,
                        ObjectName = src.Name,
                        ParentName = src.SchemaName,
                        DiffType = DiffType.Removed,
                        SourceDefinition = src
                    });
                }
            }

            // ── Added ────────────────────────────────────────────────────────
            foreach ( var (key, tgt) in tgtMap )
            {
                if ( !srcMap.ContainsKey(key) )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Synonym,
                        ObjectName = tgt.Name,
                        ParentName = tgt.SchemaName,
                        DiffType = DiffType.Added,
                        TargetDefinition = tgt
                    });
                }
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Strips brackets so [dbo].[Orders] == dbo.Orders for comparison.
        /// </summary>
        private static string NormalizeObjectName ( string name ) =>
            name.Replace("[", "").Replace("]", "").Trim().ToLowerInvariant();
    }
}