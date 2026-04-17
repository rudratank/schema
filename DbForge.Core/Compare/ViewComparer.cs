using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Text.RegularExpressions;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares views between two schemas.
    ///
    /// Pipeline
    /// ────────
    ///  1. Exact schema.name match → full definition diff → Modified or skip
    ///  2. Rename detection via body-only Jaccard similarity (≥ 0.55)
    ///     Empty bodies are excluded from rename scoring to avoid false positives.
    ///  3. Unmatched source → Removed
    ///  4. Unmatched target → Added
    /// </summary>
    public class ViewComparer
    {
        private const double RenameSimilarityThreshold = 0.55;

        public List<DiffItem> Compare ( List<ViewDefinition> source, List<ViewDefinition> target )
        {
            var diffs = new List<DiffItem>();

            static string Key ( ViewDefinition v ) => $"{v.SchemaName}.{v.Name}";
            var srcMap = source.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            var unmatchedSrc = new List<ViewDefinition>();
            var unmatchedTgt = new List<ViewDefinition>();

            // ── 1. Exact name match ──────────────────────────────────────────
            foreach ( var (key, src) in srcMap )
            {
                if ( tgtMap.TryGetValue(key, out var tgt) )
                {
                    if ( NormalizeFull(src.Definition) != NormalizeFull(tgt.Definition) )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.View,
                            ObjectName = src.Name,
                            ParentName = src.SchemaName,
                            DiffType = DiffType.Modified,
                            SourceDefinition = src,
                            TargetDefinition = tgt,
                            ChangedProperties = BuildChangeSummary(src, tgt)
                        });
                    }
                    // identical — skip
                }
                else
                {
                    unmatchedSrc.Add(src);
                }
            }

            foreach ( var (key, tgt) in tgtMap )
                if ( !srcMap.ContainsKey(key) )
                    unmatchedTgt.Add(tgt);

            // ── 2. Rename detection ──────────────────────────────────────────
            var usedTgt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var srcView in unmatchedSrc.ToList() )
            {
                string srcBody = ExtractBody(srcView.Definition);

                // Skip empty bodies — they provide no rename signal
                if ( string.IsNullOrWhiteSpace(srcBody) )
                    continue;

                var best = unmatchedTgt
                    .Where(t => !usedTgt.Contains(Key(t))
                             && !string.IsNullOrWhiteSpace(ExtractBody(t.Definition)))
                    .Select(t => new
                    {
                        View = t,
                        Score = BodySimilarity(srcBody, ExtractBody(t.Definition))
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if ( best == null || best.Score < RenameSimilarityThreshold )
                    continue;

                var changes = new List<string> { $"Renamed: {srcView.Name} → {best.View.Name}" };
                if ( NormalizeFull(srcView.Definition) != NormalizeFull(best.View.Definition) )
                    changes.AddRange(BuildChangeSummary(srcView, best.View));

                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.View,
                    ObjectName = $"{srcView.Name} → {best.View.Name}",
                    ParentName = srcView.SchemaName,
                    DiffType = DiffType.Modified,
                    SourceDefinition = srcView,
                    TargetDefinition = best.View,
                    ChangedProperties = changes
                });

                usedTgt.Add(Key(best.View));
                unmatchedSrc.Remove(srcView);
            }

            // ── 3. True Removed ──────────────────────────────────────────────
            foreach ( var v in unmatchedSrc )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.View,
                    ObjectName = v.Name,
                    ParentName = v.SchemaName,
                    DiffType = DiffType.Removed,
                    SourceDefinition = v
                });
            }

            // ── 4. True Added ────────────────────────────────────────────────
            foreach ( var v in unmatchedTgt.Where(t => !usedTgt.Contains(Key(t))) )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.View,
                    ObjectName = v.Name,
                    ParentName = v.SchemaName,
                    DiffType = DiffType.Added,
                    TargetDefinition = v
                });
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHANGE SUMMARY
        // ════════════════════════════════════════════════════════════════════

        private static List<string> BuildChangeSummary ( ViewDefinition src, ViewDefinition tgt )
        {
            var changes = new List<string>();

            if ( src.IsSchemaBound != tgt.IsSchemaBound )
                changes.Add($"SCHEMABINDING: {src.IsSchemaBound} → {tgt.IsSchemaBound}");

            if ( src.IsIndexed != tgt.IsIndexed )
                changes.Add($"Indexed (clustered): {src.IsIndexed} → {tgt.IsIndexed}");

            var srcLines = NormLineSet(ExtractBody(src.Definition));
            var tgtLines = NormLineSet(ExtractBody(tgt.Definition));
            int removed = srcLines.Count(l => !tgtLines.Contains(l));
            int added = tgtLines.Count(l => !srcLines.Contains(l));

            if ( removed > 0 ) changes.Add($"Body: {removed} line(s) removed");
            if ( added > 0 ) changes.Add($"Body: {added} line(s) added");

            if ( changes.Count == 0 )
                changes.Add("Whitespace / formatting only");

            return changes;
        }

        // ════════════════════════════════════════════════════════════════════
        // BODY EXTRACTION  (public — reused by ViewDiffBuilder)
        // ════════════════════════════════════════════════════════════════════

        public static string ExtractBody ( string sql )
        {
            if ( string.IsNullOrWhiteSpace(sql) ) return string.Empty;
            var n = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            // WITH SCHEMABINDING / ENCRYPTION option list before AS
            var m = Regex.Match(n,
                @"CREATE\s+(?:OR\s+ALTER\s+)?VIEW\s+[\w\.\[\]""` ]+\s*(?:WITH\s+[\w,\s]+)?\s*\bAS\b",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return m.Success ? n.Substring(m.Index + m.Length).Trim() : n;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        public static string NormalizeFull ( string sql ) =>
            string.IsNullOrWhiteSpace(sql)
                ? string.Empty
                : Regex.Replace(sql.Replace("\r", ""), @"\s+", " ").Trim().ToLowerInvariant();

        private static double BodySimilarity ( string a, string b )
        {
            var la = NormLineSet(a);
            var lb = NormLineSet(b);
            if ( la.Count == 0 && lb.Count == 0 ) return 1.0;
            if ( la.Count == 0 || lb.Count == 0 ) return 0.0;
            int intersection = la.Count(l => lb.Contains(l));
            int union = la.Union(lb, StringComparer.Ordinal).Count();
            return union == 0 ? 0 : ( double ) intersection / union;
        }

        private static HashSet<string> NormLineSet ( string sql ) =>
            new(
                sql.Split('\n')
                   .Select(l => Regex.Replace(l.Trim(), @"\s+", " ").ToLowerInvariant())
                   .Where(l => l.Length > 0),
                StringComparer.Ordinal);
    }
}