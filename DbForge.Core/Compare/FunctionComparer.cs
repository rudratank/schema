using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Text.RegularExpressions;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares user-defined functions (scalar, inline TVF, multi-statement TVF).
    ///
    /// Pipeline
    /// ────────
    ///  1. Exact schema.name match → compare definition → Modified or skip
    ///  2. Rename detection via body-only Jaccard similarity (≥ 0.55)
    ///     • Only functions of the same FunctionType are rename candidates.
    ///     • Empty bodies are excluded from scoring to avoid false positives.
    ///  3. Unmatched source → Removed
    ///  4. Unmatched target → Added
    ///
    /// Parameter parsing delegates to ProcedureComparer.ExtractParameters
    /// because CREATE FUNCTION uses the same @Param DataType [= default] syntax.
    /// </summary>
    public class FunctionComparer
    {
        private const double RenameSimilarityThreshold = 0.55;

        public List<DiffItem> Compare ( List<FunctionDefinition> source, List<FunctionDefinition> target )
        {
            var diffs = new List<DiffItem>();

            static string Key ( FunctionDefinition f ) => $"{f.SchemaName}.{f.Name}";
            var srcMap = source.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            var unmatchedSrc = new List<FunctionDefinition>();
            var unmatchedTgt = new List<FunctionDefinition>();

            // ── 1. Exact name match ──────────────────────────────────────────
            foreach ( var (key, src) in srcMap )
            {
                if ( tgtMap.TryGetValue(key, out var tgt) )
                {
                    if ( NormalizeFull(src.Definition) != NormalizeFull(tgt.Definition) )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Function,
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

            foreach ( var srcFn in unmatchedSrc.ToList() )
            {
                string srcBody = ExtractBody(srcFn.Definition);

                // Skip empty bodies — no reliable rename signal
                if ( string.IsNullOrWhiteSpace(srcBody) )
                    continue;

                var best = unmatchedTgt
                    .Where(t => !usedTgt.Contains(Key(t))
                             && t.FunctionType == srcFn.FunctionType  // same kind only
                             && !string.IsNullOrWhiteSpace(ExtractBody(t.Definition)))
                    .Select(t => new
                    {
                        Fn = t,
                        Score = BodySimilarity(srcBody, ExtractBody(t.Definition))
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if ( best == null || best.Score < RenameSimilarityThreshold )
                    continue;

                var changes = new List<string> { $"Renamed: {srcFn.Name} → {best.Fn.Name}" };
                if ( NormalizeFull(srcFn.Definition) != NormalizeFull(best.Fn.Definition) )
                    changes.AddRange(BuildChangeSummary(srcFn, best.Fn));

                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Function,
                    ObjectName = $"{srcFn.Name} → {best.Fn.Name}",
                    ParentName = srcFn.SchemaName,
                    DiffType = DiffType.Modified,
                    SourceDefinition = srcFn,
                    TargetDefinition = best.Fn,
                    ChangedProperties = changes
                });

                usedTgt.Add(Key(best.Fn));
                unmatchedSrc.Remove(srcFn);
            }

            // ── 3. True Removed ──────────────────────────────────────────────
            foreach ( var f in unmatchedSrc )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Function,
                    ObjectName = f.Name,
                    ParentName = f.SchemaName,
                    DiffType = DiffType.Removed,
                    SourceDefinition = f
                });
            }

            // ── 4. True Added ────────────────────────────────────────────────
            foreach ( var f in unmatchedTgt.Where(t => !usedTgt.Contains(Key(t))) )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Function,
                    ObjectName = f.Name,
                    ParentName = f.SchemaName,
                    DiffType = DiffType.Added,
                    TargetDefinition = f
                });
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHANGE SUMMARY
        // ════════════════════════════════════════════════════════════════════

        private static List<string> BuildChangeSummary ( FunctionDefinition src, FunctionDefinition tgt )
        {
            var changes = new List<string>();

            // Function kind change (rare but significant)
            if ( src.FunctionType != tgt.FunctionType )
                changes.Add($"Function type: {src.FunctionType} → {tgt.FunctionType}");

            // Return type
            if ( !string.Equals(src.ReturnType, tgt.ReturnType, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"Return type: {src.ReturnType} → {tgt.ReturnType}");

            // Parameters (reuse procedure parameter parser — same syntax)
            var srcParams = ProcedureComparer.ExtractParameters(src.Definition);
            var tgtParams = ProcedureComparer.ExtractParameters(tgt.Definition);
            var srcParamMap = srcParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var tgtParamMap = tgtParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach ( var sp in srcParams )
                if ( !tgtParamMap.ContainsKey(sp.Name) )
                    changes.Add($"Parameter removed: {sp.Name} {sp.DataType}");

            foreach ( var tp in tgtParams )
                if ( !srcParamMap.ContainsKey(tp.Name) )
                    changes.Add($"Parameter added: {tp.Name} {tp.DataType}");

            foreach ( var sp in srcParams )
            {
                if ( !tgtParamMap.TryGetValue(sp.Name, out var tp) )
                    continue;

                if ( !string.Equals(sp.DataType, tp.DataType, StringComparison.OrdinalIgnoreCase) )
                    changes.Add($"Parameter type: {sp.Name}  {sp.DataType} → {tp.DataType}");

                if ( !string.Equals(sp.DefaultValue ?? "", tp.DefaultValue ?? "", StringComparison.OrdinalIgnoreCase) )
                    changes.Add($"Parameter default: {sp.Name}  '{sp.DefaultValue ?? "none"}' → '{tp.DefaultValue ?? "none"}'");
            }

            // Body line diff
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
        // BODY EXTRACTION  (public — reused by FunctionDiffBuilder)
        // ════════════════════════════════════════════════════════════════════

        public static string ExtractBody ( string sql )
        {
            if ( string.IsNullOrWhiteSpace(sql) ) return string.Empty;
            var n = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            // Multi-statement TVF / scalar: RETURNS type AS BEGIN
            var m = Regex.Match(n,
                @"CREATE\s+(?:OR\s+ALTER\s+)?FUNCTION\s+[\w\.\[\]""` ]+.*?\bAS\b\s*\bBEGIN\b",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Inline TVF: RETURNS TABLE AS RETURN
            if ( !m.Success )
                m = Regex.Match(n,
                    @"CREATE\s+(?:OR\s+ALTER\s+)?FUNCTION\s+[\w\.\[\]""` ]+.*?\bRETURN\b",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Scalar with no BEGIN
            if ( !m.Success )
                m = Regex.Match(n,
                    @"CREATE\s+(?:OR\s+ALTER\s+)?FUNCTION\s+[\w\.\[\]""` ]+.*?\bAS\b",
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