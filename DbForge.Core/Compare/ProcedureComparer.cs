using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Text.RegularExpressions;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares stored procedures between two schemas.
    ///
    /// Pipeline
    /// ────────
    ///  1. Exact schema.name match → full definition diff → Modified or skip
    ///  2. Rename detection on unmatched procedures via body-only Jaccard (≥ 0.55)
    ///  3. Unmatched source → Removed
    ///  4. Unmatched target → Added
    ///
    /// Body-only scoring ensures that a rename (same body, different name) scores
    /// 1.0 regardless of the header change, while two unrelated procs with identical
    /// trivial bodies are kept distinct by the threshold.
    /// </summary>
    public class ProcedureComparer
    {
        private const double RenameSimilarityThreshold = 0.55;

        public List<DiffItem> Compare (
            List<ProcedureDefinition> source,
            List<ProcedureDefinition> target )
        {
            var diffs = new List<DiffItem>();

            static string Key ( ProcedureDefinition p ) => $"{p.SchemaName}.{p.Name}";

            var srcMap = source.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            var unmatchedSrc = new List<ProcedureDefinition>();
            var unmatchedTgt = new List<ProcedureDefinition>();

            // ── 1. Exact name match ──────────────────────────────────────────
            foreach ( var (key, srcProc) in srcMap )
            {
                if ( tgtMap.TryGetValue(key, out var tgtProc) )
                {
                    if ( NormalizeFull(srcProc.Definition) != NormalizeFull(tgtProc.Definition) )
                    {
                        diffs.Add(new DiffItem
                        {
                            ObjectType = ObjectType.Procedure,
                            ObjectName = srcProc.Name,
                            ParentName = srcProc.SchemaName,
                            DiffType = DiffType.Modified,
                            SourceDefinition = srcProc,
                            TargetDefinition = tgtProc,
                            ChangedProperties = BuildChangeSummary(srcProc, tgtProc)
                        });
                    }
                    // identical — skip
                }
                else
                {
                    unmatchedSrc.Add(srcProc);
                }
            }

            foreach ( var (key, tgtProc) in tgtMap )
                if ( !srcMap.ContainsKey(key) )
                    unmatchedTgt.Add(tgtProc);

            // ── 2. Rename detection — body-only Jaccard ──────────────────────
            var usedTgt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var srcProc in unmatchedSrc.ToList() )
            {
                string srcBody = ExtractBody(srcProc.Definition);

                // Guard: skip trivially empty bodies — they would match anything
                if ( string.IsNullOrWhiteSpace(srcBody) )
                    continue;

                var best = unmatchedTgt
                    .Where(t => !usedTgt.Contains(Key(t))
                             && !string.IsNullOrWhiteSpace(ExtractBody(t.Definition)))
                    .Select(t => new
                    {
                        Proc = t,
                        Score = BodySimilarity(srcBody, ExtractBody(t.Definition))
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if ( best == null || best.Score < RenameSimilarityThreshold )
                    continue;

                var changes = new List<string> { $"Renamed: {srcProc.Name} → {best.Proc.Name}" };
                if ( NormalizeFull(srcProc.Definition) != NormalizeFull(best.Proc.Definition) )
                    changes.AddRange(BuildChangeSummary(srcProc, best.Proc));

                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Procedure,
                    ObjectName = $"{srcProc.Name} → {best.Proc.Name}",
                    ParentName = srcProc.SchemaName,
                    DiffType = DiffType.Modified,
                    SourceDefinition = srcProc,
                    TargetDefinition = best.Proc,
                    ChangedProperties = changes
                });

                usedTgt.Add(Key(best.Proc));
                unmatchedSrc.Remove(srcProc);
            }

            // ── 3. True Removed ──────────────────────────────────────────────
            foreach ( var proc in unmatchedSrc )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Procedure,
                    ObjectName = proc.Name,
                    ParentName = proc.SchemaName,
                    DiffType = DiffType.Removed,
                    SourceDefinition = proc
                });
            }

            // ── 4. True Added ────────────────────────────────────────────────
            foreach ( var proc in unmatchedTgt.Where(t => !usedTgt.Contains(Key(t))) )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Procedure,
                    ObjectName = proc.Name,
                    ParentName = proc.SchemaName,
                    DiffType = DiffType.Added,
                    TargetDefinition = proc
                });
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // CHANGE SUMMARY
        // ════════════════════════════════════════════════════════════════════

        private static List<string> BuildChangeSummary (
            ProcedureDefinition src, ProcedureDefinition tgt )
        {
            var changes = new List<string>();

            var srcParams = ExtractParameters(src.Definition);
            var tgtParams = ExtractParameters(tgt.Definition);
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

                if ( sp.IsOutput != tp.IsOutput )
                    changes.Add($"Parameter direction: {sp.Name}  {(sp.IsOutput ? "OUTPUT" : "INPUT")} → {(tp.IsOutput ? "OUTPUT" : "INPUT")}");
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
        // PARAMETER EXTRACTION  (public — reused by FunctionComparer)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses @Param DataType [(size)] [= default] [OUTPUT|OUT|READONLY]
        /// from the header block of a CREATE PROCEDURE definition.
        /// </summary>
        public static List<ProcParam> ExtractParameters ( string sql )
        {
            var result = new List<ProcParam>();
            if ( string.IsNullOrWhiteSpace(sql) ) return result;

            var normalized = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            var m = Regex.Match(normalized,
                @"CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+[\w\.\[\]""` ]+\s*(.*?)\s*\bAS\b",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if ( !m.Success )
                m = Regex.Match(normalized,
                    @"CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+[\w\.\[\]""` ]+\s*(.*?)\s*\bBEGIN\b",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if ( !m.Success ) return result;

            var paramBlock = m.Groups[1].Value.Trim();
            if ( string.IsNullOrWhiteSpace(paramBlock) ) return result;

            foreach ( var raw in SplitOnComma(paramBlock) )
            {
                var p = ParseSingleParam(raw.Trim());
                if ( p != null ) result.Add(p);
            }

            return result;
        }

        private static ProcParam? ParseSingleParam ( string raw )
        {
            if ( string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith("@") )
                return null;

            var m = Regex.Match(raw,
                @"(@\w+)\s+(\w+(?:\s*\(\s*[\w\s,]+\s*\))?(?:\s+\w+(?:\s*\([\w\s,]+\))?)*?)" +
                @"(?:\s*=\s*([^\s,]+))?(?:\s+(OUTPUT|OUT|READONLY))?\s*$",
                RegexOptions.IgnoreCase);

            if ( !m.Success ) return null;

            return new ProcParam
            {
                Name = m.Groups[1].Value.Trim(),
                DataType = m.Groups[2].Value.Trim(),
                DefaultValue = m.Groups[3].Success ? m.Groups[3].Value.Trim() : null,
                IsOutput = m.Groups[4].Success &&
                           !m.Groups[4].Value.Equals("READONLY", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static IEnumerable<string> SplitOnComma ( string s )
        {
            int depth = 0, start = 0;
            for ( int i = 0; i < s.Length; i++ )
            {
                if ( s[i] == '(' ) depth++;
                else if ( s[i] == ')' ) depth--;
                else if ( s[i] == ',' && depth == 0 )
                {
                    yield return s.Substring(start, i - start);
                    start = i + 1;
                }
            }
            if ( start < s.Length )
                yield return s.Substring(start);
        }

        // ════════════════════════════════════════════════════════════════════
        // BODY EXTRACTION  (public — reused by FunctionComparer + diff builders)
        // ════════════════════════════════════════════════════════════════════

        public static string ExtractBody ( string sql )
        {
            if ( string.IsNullOrWhiteSpace(sql) ) return string.Empty;

            var normalized = sql.Replace("\r\n", "\n").Replace("\r", "\n");

            var m = Regex.Match(normalized,
                @"CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+[\w\.\[\]""` ]+.*?\bAS\b",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if ( !m.Success )
                m = Regex.Match(normalized,
                    @"CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+[\w\.\[\]""` ]+.*?\bBEGIN\b",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if ( !m.Success ) return normalized;

            return normalized.Substring(m.Index + m.Length).Trim();
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static double BodySimilarity ( string bodyA, string bodyB )
        {
            var linesA = NormLineSet(bodyA);
            var linesB = NormLineSet(bodyB);
            if ( linesA.Count == 0 && linesB.Count == 0 ) return 1.0;
            if ( linesA.Count == 0 || linesB.Count == 0 ) return 0.0;
            int intersection = linesA.Count(l => linesB.Contains(l));
            int union = linesA.Union(linesB, StringComparer.Ordinal).Count();
            return union == 0 ? 0 : ( double ) intersection / union;
        }

        private static HashSet<string> NormLineSet ( string sql ) =>
            new(
                sql.Split('\n')
                   .Select(l => Regex.Replace(l.Trim(), @"\s+", " ").ToLowerInvariant())
                   .Where(l => l.Length > 0),
                StringComparer.Ordinal);

        public static string NormalizeFull ( string sql ) =>
            string.IsNullOrWhiteSpace(sql)
                ? string.Empty
                : Regex.Replace(sql.Replace("\r", ""), @"\s+", " ").Trim().ToLowerInvariant();
    }
}