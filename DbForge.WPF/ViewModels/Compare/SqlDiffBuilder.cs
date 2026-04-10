using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    public static class SqlDiffBuilder
    {
        // ══════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ══════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            TableDefinition? srcTable,
            TableDefinition? tgtTable,
            IReadOnlyList<DiffItem> columnDiffs )
        {
            var pairs = new List<SqlDiffPair>();

            if ( srcTable == null && tgtTable == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No table selected"),
                    SqlDiffLine.Context("-- No table selected")));
                return pairs;
            }

            pairs.Add(SqlDiffPair.Header("── Table Definition ──────────────────────────────────────────"));
            BuildTableHeaderLines(pairs, srcTable, tgtTable);

            pairs.Add(SqlDiffPair.Header("── Columns ───────────────────────────────────────────────────"));
            BuildColumnLines(pairs, srcTable, tgtTable, columnDiffs);

            pairs.Add(SqlDiffPair.Header("── Indexes ───────────────────────────────────────────────────"));
            BuildIndexLines(pairs, srcTable, tgtTable, columnDiffs);

            pairs.Add(SqlDiffPair.Header("── Foreign Keys ──────────────────────────────────────────────"));
            BuildForeignKeyLines(pairs, srcTable, tgtTable, columnDiffs);

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            TableDefinition? srcTable,
            TableDefinition? tgtTable,
            IReadOnlyList<DiffItem> columnDiffs,
            string tableName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            var tableStatus = srcTable == null ? "Added"
                            : tgtTable == null ? "Removed"
                            : columnDiffs.Any() ? "Modified"
                            : "Identical";

            var tableNode = new DiffTreeNode
            {
                Label = tableName,
                Icon = "⊞",
                NodeKind = "Table",
                DiffStatus = tableStatus,
                IsExpanded = true
            };

            // ── Columns sub-tree ──────────────────────────────────────────────
            var colSectionNode = new DiffTreeNode
            {
                Label = "Columns",
                Icon = "≡",
                NodeKind = "Columns",
                DiffStatus = columnDiffs.Any(d => d.ObjectType == ObjectType.Column) ? "Modified" : "Identical"
            };

            var colDiffs = columnDiffs.Where(d => d.ObjectType == ObjectType.Column).ToList();
            var allColNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var c in srcTable?.Columns ?? Enumerable.Empty<ColumnDefinition>() ) allColNames.Add(c.Name);
            foreach ( var c in tgtTable?.Columns ?? Enumerable.Empty<ColumnDefinition>() ) allColNames.Add(c.Name);

            foreach ( var colName in allColNames.OrderBy(n => n) )
            {
                var diff = colDiffs.FirstOrDefault(d =>
    d.ObjectName.Equals(colName, StringComparison.OrdinalIgnoreCase) ||
    (d.TargetDefinition as ColumnDefinition)?.Name.Equals(colName, StringComparison.OrdinalIgnoreCase) == true
);
                var status = diff == null ? "Identical"
                           : diff.DiffType == DiffType.Added ? "Added"
                           : diff.DiffType == DiffType.Removed ? "Removed"
                           : "Modified";

                colSectionNode.Children.Add(new DiffTreeNode
                {
                    Label = FormatColumnLabel(colName, srcTable, tgtTable),
                    Icon = "▪",
                    NodeKind = "Column",
                    DiffStatus = status
                });
            }

            tableNode.Children.Add(colSectionNode);
            tableNode.Children.Add(BuildIndexTreeSection(srcTable, tgtTable, columnDiffs));
            tableNode.Children.Add(BuildFkTreeSection(srcTable, tgtTable, columnDiffs));

            root.Add(tableNode);
            return root;
        }

        // ══════════════════════════════════════════════════════════════════════
        // INLINE SEGMENT COMPUTATION  (public so tests can call it)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes token-level inline diff segments for a pair of Modified lines.
        /// Unchanged tokens → IsHighlighted = false.
        /// Changed tokens   → IsHighlighted = true (rendered with background colour).
        /// Text colour is never changed here — that stays the same for all tokens.
        /// </summary>
        public static (IReadOnlyList<DiffSegment> SrcSegments,
                        IReadOnlyList<DiffSegment> TgtSegments)
            ComputeInlineSegments ( string srcText, string tgtText )
        {
            if ( srcText == tgtText )
            {
                var same = new[] { new DiffSegment { Text = srcText } };
                return (same, same);
            }

            var srcToks = TokenizeDDL(srcText);
            var tgtToks = TokenizeDDL(tgtText);

            var (srcMarked, tgtMarked) = DiffTokenLCS(srcToks, tgtToks);

            return (MergeSegments(srcMarked), MergeSegments(tgtMarked));
        }

        // ══════════════════════════════════════════════════════════════════════
        // DIFF PAIR BUILDERS
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildTableHeaderLines (
            List<SqlDiffPair> pairs, TableDefinition? src, TableDefinition? tgt )
        {
            var srcHeader = src != null ? $"CREATE TABLE [{src.SchemaName}].[{src.Name}]" : string.Empty;
            var tgtHeader = tgt != null ? $"CREATE TABLE [{tgt.SchemaName}].[{tgt.Name}]" : string.Empty;

            if ( src == null )
                pairs.Add(SqlDiffPair.TargetOnly(tgtHeader));
            else if ( tgt == null )
                pairs.Add(SqlDiffPair.SourceOnly(srcHeader));
            else
                pairs.Add(SqlDiffPair.Unchanged(srcHeader, 1, 1));
        }

        private static void BuildColumnLines (
           List<SqlDiffPair> pairs,
           TableDefinition? src,
           TableDefinition? tgt,
           IReadOnlyList<DiffItem> diffs )
        {
            if ( src == null && tgt == null ) return;

            // ── Source only ─────────────────────────────
            if ( tgt == null && src != null )
            {
                foreach ( var col in src.Columns.OrderBy(c => c.OrdinalPosition) )
                    pairs.Add(SqlDiffPair.SourceOnly(FormatColumnDDL(col)));
                return;
            }

            // ── Target only ─────────────────────────────
            if ( src == null && tgt != null )
            {
                foreach ( var col in tgt.Columns.OrderBy(c => c.OrdinalPosition) )
                    pairs.Add(SqlDiffPair.TargetOnly(FormatColumnDDL(col)));
                return;
            }

            // ✅ Build maps (NO duplicates possible)
            var srcMap = src.Columns
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var tgtMap = tgt.Columns
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // ✅ Filter column diffs
            var colDiffs = diffs
                .Where(d => d.ObjectType == ObjectType.Column)
                .ToList();
            var renames = diffs
                .Where(d => d.DiffType == DiffType.Modified
                         && d.SourceDefinition is ColumnDefinition
                         && d.TargetDefinition is ColumnDefinition)
                .Select(d => (
                    src: ( ColumnDefinition ) d.SourceDefinition!,
                    tgt: ( ColumnDefinition ) d.TargetDefinition!))
                .Where(r => !r.src.Name.Equals(r.tgt.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // ✅ Build ordered column list (SOURCE first, then TARGET extras)
            var rows = new List<(ColumnDefinition? src, ColumnDefinition? tgt)>();

            var matchedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Match source → target
            foreach ( var s in src.Columns.OrderBy(c => c.OrdinalPosition) )
            {
                // try exact match
                var t = tgt.Columns.FirstOrDefault(x =>
                    x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));

                // try rename match
                if ( t == null )
                {
                    var rename = renames.FirstOrDefault(r =>
                    r.src.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));

                    if ( rename != default )
                    {
                        t = rename.tgt;
                    }
                }

                if ( t != null )
                    matchedTargets.Add(t.Name);

                rows.Add((s, t));
            }

            // 2. Add remaining target-only columns
            foreach ( var t in tgt.Columns.OrderBy(c => c.OrdinalPosition) )
            {
                if ( !matchedTargets.Contains(t.Name) )
                    rows.Add((null, t));
            }

            // ✅ Final rendering loop (STRICT single pass)
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var (srcCol, tgtCol) in rows )
            {
                var srcText = srcCol != null ? FormatColumnDDL(srcCol) : string.Empty;
                var tgtText = tgtCol != null ? FormatColumnDDL(tgtCol) : string.Empty;

                // detect rename
                bool isRename = srcCol != null && tgtCol != null &&
                                !srcCol.Name.Equals(tgtCol.Name, StringComparison.OrdinalIgnoreCase);

                if ( isRename )
                {
                    var (srcSegs, tgtSegs) = ComputeInlineSegments(srcText, tgtText);

                    pairs.Add(SqlDiffPair.Both(
                        SqlDiffLine.Modified(srcText, segments: srcSegs),
                        SqlDiffLine.Modified(tgtText, segments: tgtSegs)));

                    continue;
                }

                if ( srcCol != null && tgtCol != null )
                {
                    if ( srcText == tgtText )
                    {
                        pairs.Add(SqlDiffPair.Unchanged(
                            srcText,
                            srcCol.OrdinalPosition,
                            tgtCol.OrdinalPosition));
                    }
                    else
                    {
                        var (srcSegs, tgtSegs) = ComputeInlineSegments(srcText, tgtText);

                        pairs.Add(SqlDiffPair.Both(
                            SqlDiffLine.Modified(srcText, segments: srcSegs),
                            SqlDiffLine.Modified(tgtText, segments: tgtSegs)));
                    }
                }
                else if ( srcCol != null )
                {
                    pairs.Add(SqlDiffPair.SourceOnly(srcText));
                }
                else if ( tgtCol != null )
                {
                    pairs.Add(SqlDiffPair.TargetOnly(tgtText));
                }
            }
        }

        private static void BuildIndexLines (
            List<SqlDiffPair> pairs,
            TableDefinition? src,
            TableDefinition? tgt,
            IReadOnlyList<DiffItem> diffs )
        {
            var idxDiffs = diffs
                .Where(d => d.ObjectType == ObjectType.Index)
                .ToDictionary(d => d.ObjectName, StringComparer.OrdinalIgnoreCase);

            var srcMap = src?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, IndexDefinition>();
            var tgtMap = tgt?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, IndexDefinition>();

            var allNames = srcMap.Keys.Union(tgtMap.Keys, StringComparer.OrdinalIgnoreCase)
                                      .OrderBy(n => n).ToList();

            if ( !allNames.Any() )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("    -- No indexes"),
                    SqlDiffLine.Context("    -- No indexes")));
                return;
            }

            foreach ( var name in allNames )
            {
                idxDiffs.TryGetValue(name, out var diff);
                srcMap.TryGetValue(name, out var srcIdx);
                tgtMap.TryGetValue(name, out var tgtIdx);

                if ( diff == null && srcIdx != null && tgtIdx != null )
                {
                    pairs.Add(SqlDiffPair.Unchanged(FormatIndexDDL(srcIdx, src!.Name), 0, 0));
                    continue;
                }

                if ( srcIdx != null && tgtIdx == null )
                {
                    pairs.Add(SqlDiffPair.SourceOnly(FormatIndexDDL(srcIdx, src!.Name)));
                    continue;
                }

                if ( srcIdx == null && tgtIdx != null )
                {
                    pairs.Add(SqlDiffPair.TargetOnly(FormatIndexDDL(tgtIdx, tgt!.Name)));
                    continue;
                }

                // Both exist, definition differs → inline highlights
                if ( srcIdx != null && tgtIdx != null )
                {
                    var srcText = FormatIndexDDL(srcIdx, src!.Name);
                    var tgtText = FormatIndexDDL(tgtIdx, tgt!.Name);
                    var (srcSegs, tgtSegs) = ComputeInlineSegments(srcText, tgtText);
                    pairs.Add(SqlDiffPair.Both(
                        SqlDiffLine.Modified(srcText, segments: srcSegs),
                        SqlDiffLine.Modified(tgtText, segments: tgtSegs)));
                }
            }
        }

        private static void BuildForeignKeyLines (
            List<SqlDiffPair> pairs,
            TableDefinition? src,
            TableDefinition? tgt,
            IReadOnlyList<DiffItem> diffs )
        {
            var fkDiffs = diffs
                .Where(d => d.ObjectType == ObjectType.ForeignKey)
                .ToDictionary(d => d.ObjectName, StringComparer.OrdinalIgnoreCase);

            var srcMap = src?.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, ForeignKeyDefinition>();
            var tgtMap = tgt?.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, ForeignKeyDefinition>();

            var allNames = srcMap.Keys.Union(tgtMap.Keys, StringComparer.OrdinalIgnoreCase)
                                      .OrderBy(n => n).ToList();

            if ( !allNames.Any() )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("    -- No foreign keys"),
                    SqlDiffLine.Context("    -- No foreign keys")));
                return;
            }

            foreach ( var name in allNames )
            {
                fkDiffs.TryGetValue(name, out var diff);
                srcMap.TryGetValue(name, out var srcFk);
                tgtMap.TryGetValue(name, out var tgtFk);

                if ( diff == null && srcFk != null && tgtFk != null )
                {
                    pairs.Add(SqlDiffPair.Unchanged(FormatFkDDL(srcFk, src!.Name), 0, 0));
                    continue;
                }

                if ( srcFk != null && tgtFk == null )
                {
                    pairs.Add(SqlDiffPair.SourceOnly(FormatFkDDL(srcFk, src!.Name)));
                    continue;
                }

                if ( srcFk == null && tgtFk != null )
                {
                    pairs.Add(SqlDiffPair.TargetOnly(FormatFkDDL(tgtFk, tgt!.Name)));
                    continue;
                }

                if ( srcFk != null && tgtFk != null )
                {
                    var srcText = FormatFkDDL(srcFk, src!.Name);
                    var tgtText = FormatFkDDL(tgtFk, tgt!.Name);
                    var (srcSegs, tgtSegs) = ComputeInlineSegments(srcText, tgtText);
                    pairs.Add(SqlDiffPair.Both(
                        SqlDiffLine.Modified(srcText, segments: srcSegs),
                        SqlDiffLine.Modified(tgtText, segments: tgtSegs)));
                }
            }
        }



        // ══════════════════════════════════════════════════════════════════════
        // TREE NODE BUILDERS  (unchanged logic, kept here for completeness)
        // ══════════════════════════════════════════════════════════════════════

        private static List<(ColumnDefinition src, ColumnDefinition tgt)> DetectRenames (
    List<ColumnDefinition> srcCols,
    List<ColumnDefinition> tgtCols )
        {
            var matches = new List<(ColumnDefinition src, ColumnDefinition tgt)>();
            var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ✅ STEP 1: EXACT STRUCTURE MATCH (strict)
            foreach ( var s in srcCols )
            {
                var exact = tgtCols.FirstOrDefault(t =>
                    !usedTargets.Contains(t.Name) &&
                    s.FullDataType.Equals(t.FullDataType, StringComparison.OrdinalIgnoreCase) &&
                    s.IsNullable == t.IsNullable &&
                    s.OrdinalPosition == t.OrdinalPosition // 🔥 IMPORTANT
                );

                if ( exact != null && !s.Name.Equals(exact.Name, StringComparison.OrdinalIgnoreCase) )
                {
                    matches.Add((s, exact));
                    usedTargets.Add(exact.Name);
                }
            }

            // ✅ STEP 2: FALLBACK (fuzzy only if needed)
            foreach ( var s in srcCols )
            {
                if ( matches.Any(m => m.src == s) )
                    continue;

                var candidates = tgtCols
                    .Where(t => !usedTargets.Contains(t.Name))
                    .Select(t => new
                    {
                        tgt = t,
                        score =
                            (s.FullDataType.Equals(t.FullDataType, StringComparison.OrdinalIgnoreCase) ? 50 : 0) +
                            (s.IsNullable == t.IsNullable ? 20 : 0) +
                            (LevenshteinSimilarity(s.Name, t.Name) * 30) // 🔥 stronger name weight
                    })
                    .OrderByDescending(x => x.score)
                    .FirstOrDefault();

                if ( candidates != null && candidates.score >= 80 )
                {
                    matches.Add((s, candidates.tgt));
                    usedTargets.Add(candidates.tgt.Name);
                }
            }

            return matches;
        }


        private static double LevenshteinSimilarity ( string a, string b )
        {
            int dist = LevenshteinDistance(a, b);
            int maxLen = Math.Max(a.Length, b.Length);
            return maxLen == 0 ? 1 : 1.0 - ( double ) dist / maxLen;
        }

        private static int LevenshteinDistance ( string a, string b )
        {
            if ( string.IsNullOrEmpty(a) ) return b?.Length ?? 0;
            if ( string.IsNullOrEmpty(b) ) return a.Length;

            int[,] dp = new int[a.Length + 1, b.Length + 1];

            for ( int i = 0; i <= a.Length; i++ )
                dp[i, 0] = i;

            for ( int j = 0; j <= b.Length; j++ )
                dp[0, j] = j;

            for ( int i = 1; i <= a.Length; i++ )
            {
                for ( int j = 1; j <= b.Length; j++ )
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1,     // delete
                                 dp[i, j - 1] + 1),    // insert
                        dp[i - 1, j - 1] + cost       // replace
                    );
                }
            }

            return dp[a.Length, b.Length];
        }

        private static DiffTreeNode BuildIndexTreeSection (
            TableDefinition? src, TableDefinition? tgt, IReadOnlyList<DiffItem> diffs )
        {
            var idxDiffs = diffs.Where(d => d.ObjectType == ObjectType.Index)
                                .ToDictionary(d => d.ObjectName, StringComparer.OrdinalIgnoreCase);
            var section = new DiffTreeNode
            {
                Label = "Indexes",
                Icon = "◈",
                NodeKind = "Indexes",
                DiffStatus = idxDiffs.Any() ? "Modified" : "Identical"
            };

            var srcMap = src?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, IndexDefinition>();
            var tgtMap = tgt?.Indexes.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, IndexDefinition>();

            foreach ( var name in srcMap.Keys.Union(tgtMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n) )
            {
                idxDiffs.TryGetValue(name, out var diff);
                var status = diff == null ? "Identical"
                           : diff.DiffType == DiffType.Added ? "Added"
                           : diff.DiffType == DiffType.Removed ? "Removed"
                           : "Modified";
                section.Children.Add(new DiffTreeNode
                {
                    Label = name,
                    Icon = srcMap.TryGetValue(name, out var idx) && idx.IsPrimaryKey ? "🔑" : "◇",
                    NodeKind = "Index",
                    DiffStatus = status
                });
            }
            return section;
        }

        private static DiffTreeNode BuildFkTreeSection (
            TableDefinition? src, TableDefinition? tgt, IReadOnlyList<DiffItem> diffs )
        {
            var fkDiffs = diffs.Where(d => d.ObjectType == ObjectType.ForeignKey)
                               .ToDictionary(d => d.ObjectName, StringComparer.OrdinalIgnoreCase);
            var section = new DiffTreeNode
            {
                Label = "Foreign Keys",
                Icon = "🔗",
                NodeKind = "ForeignKeys",
                DiffStatus = fkDiffs.Any() ? "Modified" : "Identical"
            };

            var srcMap = src?.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, ForeignKeyDefinition>();
            var tgtMap = tgt?.ForeignKeys.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase)
                         ?? new Dictionary<string, ForeignKeyDefinition>();

            foreach ( var name in srcMap.Keys.Union(tgtMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n) )
            {
                fkDiffs.TryGetValue(name, out var diff);
                var status = diff == null ? "Identical"
                           : diff.DiffType == DiffType.Added ? "Added"
                           : diff.DiffType == DiffType.Removed ? "Removed"
                           : "Modified";
                section.Children.Add(new DiffTreeNode
                {
                    Label = name,
                    Icon = "⛓",
                    NodeKind = "ForeignKey",
                    DiffStatus = status
                });
            }
            return section;
        }

        // ══════════════════════════════════════════════════════════════════════
        // INLINE DIFF HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Splits a SQL DDL string into meaningful tokens.
        /// Identifiers/numbers → single tokens.
        /// Leading whitespace  → grouped as one token.
        /// All other chars     → individual tokens.
        /// </summary>
        private static List<string> TokenizeDDL ( string text )
        {
            var tokens = new List<string>();
            if ( string.IsNullOrEmpty(text) ) return tokens;

            int i = 0;
            var sb = new System.Text.StringBuilder();

            // Group leading whitespace as one token (indentation, never highlighted)
            while ( i < text.Length && text[i] == ' ' )
            {
                sb.Append(text[i]);
                i++;
            }
            if ( sb.Length > 0 )
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }

            while ( i < text.Length )
            {
                char c = text[i];

                if ( char.IsLetterOrDigit(c) || c == '_' )
                {
                    sb.Append(c);
                    i++;
                }
                else
                {
                    if ( sb.Length > 0 ) { tokens.Add(sb.ToString()); sb.Clear(); }

                    if ( c == ' ' )
                    {
                        // Inline spaces: group consecutive spaces
                        while ( i < text.Length && text[i] == ' ' )
                        {
                            sb.Append(text[i]);
                            i++;
                        }
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        tokens.Add(c.ToString());
                        i++;
                    }
                }
            }

            if ( sb.Length > 0 ) tokens.Add(sb.ToString());
            return tokens;
        }

        /// <summary>
        /// Token-level LCS diff: returns two lists of (token, isChanged) pairs.
        /// </summary>
        private static (List<(string tok, bool changed)> src, List<(string tok, bool changed)> tgt)
            DiffTokenLCS ( List<string> srcToks, List<string> tgtToks )
        {
            int m = srcToks.Count, n = tgtToks.Count;
            var dp = new int[m + 1, n + 1];

            for ( int r = 1; r <= m; r++ )
                for ( int c = 1; c <= n; c++ )
                    dp[r, c] = srcToks[r - 1] == tgtToks[c - 1]
                        ? dp[r - 1, c - 1] + 1
                        : Math.Max(dp[r - 1, c], dp[r, c - 1]);

            // Backtrack
            var srcOps = new List<(string, bool)>();
            var tgtOps = new List<(string, bool)>();
            int si = m, ti = n;

            while ( si > 0 || ti > 0 )
            {
                if ( si > 0 && ti > 0 && srcToks[si - 1] == tgtToks[ti - 1] )
                {
                    srcOps.Add((srcToks[si - 1], false));
                    tgtOps.Add((tgtToks[ti - 1], false));
                    si--; ti--;
                }
                else if ( ti > 0 && (si == 0 || dp[si, ti - 1] >= dp[si - 1, ti]) )
                {
                    tgtOps.Add((tgtToks[ti - 1], true));
                    ti--;
                }
                else
                {
                    srcOps.Add((srcToks[si - 1], true));
                    si--;
                }
            }

            srcOps.Reverse();
            tgtOps.Reverse();
            return (srcOps, tgtOps);
        }

        /// <summary>
        /// Merges consecutive same-highlight tokens into single DiffSegment objects.
        /// </summary>
        private static List<DiffSegment> MergeSegments ( List<(string tok, bool changed)> ops )
        {
            var result = new List<DiffSegment>();
            if ( ops.Count == 0 ) return result;

            var sb = new System.Text.StringBuilder();
            bool curHl = ops[0].changed;

            foreach ( var (tok, changed) in ops )
            {
                if ( changed == curHl )
                {
                    sb.Append(tok);
                }
                else
                {
                    result.Add(new DiffSegment { Text = sb.ToString(), IsHighlighted = curHl });
                    sb.Clear();
                    sb.Append(tok);
                    curHl = changed;
                }
            }

            if ( sb.Length > 0 )
                result.Add(new DiffSegment { Text = sb.ToString(), IsHighlighted = curHl });

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        // DDL FORMATTERS
        // ══════════════════════════════════════════════════════════════════════

        private static string FormatColumnDDL ( ColumnDefinition col )
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"    [{col.Name}]");
            sb.Append($" {col.FullDataType}");
            sb.Append(col.IsNullable ? " NULL" : " NOT NULL");
            if ( col.IsIdentity ) sb.Append(" IDENTITY(1,1)");
            if ( col.IsPrimaryKey ) sb.Append(" PRIMARY KEY");
            if ( !string.IsNullOrEmpty(col.DefaultValue) )
                sb.Append($" DEFAULT {col.DefaultValue}");
            return sb.ToString();
        }

        private static string FormatIndexDDL ( IndexDefinition idx, string tableName )
        {
            if ( idx.IsPrimaryKey )
            {
                var cols = string.Join(", ",
                    idx.Columns.OrderBy(c => c.Position).Select(c => $"[{c.ColumnName}]"));
                return $"    ALTER TABLE [{tableName}] ADD CONSTRAINT [{idx.Name}] PRIMARY KEY ({cols})";
            }

            var kind = idx.IsUnique ? "UNIQUE " : "";
            var cluster = idx.IsClustered ? "CLUSTERED " : "NONCLUSTERED ";
            var idxCols = string.Join(", ",
                idx.Columns.OrderBy(c => c.Position)
                           .Select(c => $"[{c.ColumnName}]{(c.Descending ? " DESC" : "")}"));
            return $"    CREATE {kind}{cluster}INDEX [{idx.Name}] ON [{tableName}] ({idxCols})";
        }

        private static string FormatFkDDL ( ForeignKeyDefinition fk, string tableName )
        {
            var cols = string.Join(", ", fk.Columns.Select(c => $"[{c}]"));
            var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"[{c}]"));
            return $"    ALTER TABLE [{tableName}] ADD CONSTRAINT [{fk.Name}] " +
                   $"FOREIGN KEY ({cols}) REFERENCES [{fk.ReferencedTable}] ({refCols}) " +
                   $"ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate}";
        }

        private static string FormatColumnLabel ( string name, TableDefinition? src, TableDefinition? tgt )
        {
            var col = src?.Columns.FirstOrDefault(c =>
                          c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? tgt?.Columns.FirstOrDefault(c =>
                          c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return col != null ? $"{name}  ({col.FullDataType})" : name;
        }

    }
}