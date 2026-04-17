using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Text.RegularExpressions;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares DML triggers between two schemas.
    ///
    /// Pipeline
    /// ────────
    ///  1. Exact schema.name match → property + body diff → Modified or skip
    ///  2. Rename detection via body-only Jaccard (≥ 0.60)
    ///     • Only triggers with the same Timing AND Events are rename candidates.
    ///     • Empty bodies are excluded from scoring.
    ///  3. Unmatched source → Removed
    ///  4. Unmatched target → Added
    ///
    /// Rename threshold is intentionally higher (0.60) than procedures/views
    /// because trigger names are often table-derived, making body similarity
    /// a stronger and more reliable rename signal.
    /// </summary>
    public class TriggerComparer
    {
        private const double RenameSimilarityThreshold = 0.60;

        public List<DiffItem> Compare ( List<TriggerDefinition> source, List<TriggerDefinition> target )
        {
            var diffs = new List<DiffItem>();

            static string Key ( TriggerDefinition t ) => $"{t.SchemaName}.{t.Name}";
            var srcMap = source.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            var unmatchedSrc = new List<TriggerDefinition>();
            var unmatchedTgt = new List<TriggerDefinition>();

            // ── 1. Exact name match ──────────────────────────────────────────
            foreach ( var (key, src) in srcMap )
            {
                if ( tgtMap.TryGetValue(key, out var tgt) )
                {
                    var changes = BuildChangeSummary(src, tgt);
                    if ( changes.Count > 0 )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Trigger,
                            ObjectName = src.Name,
                            ParentName = src.ParentTable,
                            DiffType = DiffType.Modified,
                            SourceDefinition = src,
                            TargetDefinition = tgt,
                            ChangedProperties = changes
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

            foreach ( var srcTr in unmatchedSrc.ToList() )
            {
                string srcBody = ExtractBody(srcTr.Definition);

                // Skip empty bodies — no reliable rename signal
                if ( string.IsNullOrWhiteSpace(srcBody) )
                    continue;

                var best = unmatchedTgt
                    .Where(t => !usedTgt.Contains(Key(t))
                             && t.Timing == srcTr.Timing   // AFTER / INSTEAD OF must match
                             && t.Events == srcTr.Events   // event mask must match
                             && !string.IsNullOrWhiteSpace(ExtractBody(t.Definition)))
                    .Select(t => new
                    {
                        Tr = t,
                        Score = BodySimilarity(srcBody, ExtractBody(t.Definition))
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if ( best == null || best.Score < RenameSimilarityThreshold )
                    continue;

                // Rename entry — suppress body line counts (implicit in the rename)
                var changes = new List<string> { $"Renamed: {srcTr.Name} → {best.Tr.Name}" };
                changes.AddRange(
                    BuildChangeSummary(srcTr, best.Tr)
                        .Where(c => !c.StartsWith("Body:")));

                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Trigger,
                    ObjectName = $"{srcTr.Name} → {best.Tr.Name}",
                    ParentName = srcTr.ParentTable,
                    DiffType = DiffType.Modified,
                    SourceDefinition = srcTr,
                    TargetDefinition = best.Tr,
                    ChangedProperties = changes
                });

                usedTgt.Add(Key(best.Tr));
                unmatchedSrc.Remove(srcTr);
            }

            // ── 3. True Removed ──────────────────────────────────────────────
            foreach ( var t in unmatchedSrc )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Trigger,
                    ObjectName = t.Name,
                    ParentName = t.ParentTable,
                    DiffType = DiffType.Removed,
                    SourceDefinition = t
                });
            }

            // ── 4. True Added ────────────────────────────────────────────────
            foreach ( var t in unmatchedTgt.Where(x => !usedTgt.Contains(Key(x))) )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Trigger,
                    ObjectName = t.Name,
                    ParentName = t.ParentTable,
                    DiffType = DiffType.Added,
                    TargetDefinition = t
                });
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHANGE SUMMARY
        // ════════════════════════════════════════════════════════════════════

        private static List<string> BuildChangeSummary ( TriggerDefinition src, TriggerDefinition tgt )
        {
            var changes = new List<string>();

            if ( src.IsEnabled != tgt.IsEnabled )
                changes.Add($"Enabled: {src.IsEnabled} → {tgt.IsEnabled}");

            if ( src.Timing != tgt.Timing )
                changes.Add($"Timing: {src.Timing} → {tgt.Timing}");

            if ( src.Events != tgt.Events )
                changes.Add($"Events: [{FormatEvents(src.Events)}] → [{FormatEvents(tgt.Events)}]");

            if ( !string.Equals(src.ParentTable, tgt.ParentTable, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"Parent table: {src.ParentTable} → {tgt.ParentTable}");

            // Body diff
            if ( NormalizeFull(src.Definition) != NormalizeFull(tgt.Definition) )
            {
                var srcLines = NormLineSet(ExtractBody(src.Definition));
                var tgtLines = NormLineSet(ExtractBody(tgt.Definition));
                int removed = srcLines.Count(l => !tgtLines.Contains(l));
                int added = tgtLines.Count(l => !srcLines.Contains(l));

                if ( removed > 0 ) changes.Add($"Body: {removed} line(s) removed");
                if ( added > 0 ) changes.Add($"Body: {added} line(s) added");

                if ( removed == 0 && added == 0 )
                    changes.Add("Whitespace / formatting only");
            }

            return changes;
        }

        private static string FormatEvents ( TriggerEvents e )
        {
            var parts = new List<string>();
            if ( e.HasFlag(TriggerEvents.Insert) ) parts.Add("INSERT");
            if ( e.HasFlag(TriggerEvents.Update) ) parts.Add("UPDATE");
            if ( e.HasFlag(TriggerEvents.Delete) ) parts.Add("DELETE");
            return parts.Count > 0 ? string.Join(", ", parts) : "NONE";
        }

        // ════════════════════════════════════════════════════════════════════
        // BODY EXTRACTION  (public — reused by TriggerDiffBuilder)
        // ════════════════════════════════════════════════════════════════════

        public static string ExtractBody ( string sql )
        {
            if ( string.IsNullOrWhiteSpace(sql) ) return string.Empty;
            var n = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            // CREATE [OR ALTER] TRIGGER name ON table [WITH ...] AFTER|INSTEAD OF ... AS
            var m = Regex.Match(n,
                @"CREATE\s+(?:OR\s+ALTER\s+)?TRIGGER\s+[\w\.\[\]""` ]+.*?\bAS\b",
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