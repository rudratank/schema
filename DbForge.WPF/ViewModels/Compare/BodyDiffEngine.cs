using System.Text.RegularExpressions;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Shared LCS-based diff engine used by ViewDiffBuilder, FunctionDiffBuilder,
    /// TriggerDiffBuilder, and ProcedureDiffBuilder.
    ///
    /// Consumers call AppendBodyDiff() to push SqlDiffPair rows into a list.
    /// The Modified-pair merge heuristic (TokenOverlap ≥ 40%) is identical to
    /// the original ProcedureDiffBuilder algorithm.
    /// </summary>
    internal static class BodyDiffEngine
    {
        private const double ModifiedPairThreshold = 0.40;

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs an LCS diff on <paramref name="srcBody"/> vs <paramref name="tgtBody"/>
        /// and appends SqlDiffPair rows (with inline token highlights for Modified pairs)
        /// to <paramref name="pairs"/>.
        /// </summary>
        public static void AppendBodyDiff (
            List<SqlDiffPair> pairs,
            string srcBody,
            string tgtBody )
        {
            var srcLines = SplitLines(srcBody);
            var tgtLines = SplitLines(tgtBody);

            if ( srcLines.Count == 0 && tgtLines.Count == 0 )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("    -- Empty body"),
                    SqlDiffLine.Context("    -- Empty body")));
                return;
            }

            var editScript = ComputeEditScript(srcLines, tgtLines);
            var merged = MergeModifiedPairs(editScript);

            int srcNo = 1, tgtNo = 1;

            foreach ( var op in merged )
            {
                switch ( op.Kind )
                {
                    case OpKind.Context:
                        pairs.Add(SqlDiffPair.Unchanged(op.SrcText!, srcNo++, tgtNo++));
                        break;
                    case OpKind.Removed:
                        pairs.Add(SqlDiffPair.SourceOnly(op.SrcText!, srcNo++));
                        break;
                    case OpKind.Added:
                        pairs.Add(SqlDiffPair.TargetOnly(op.TgtText!, tgtNo++));
                        break;
                    case OpKind.Modified:
                        var (srcSegs, tgtSegs) =
                            SqlDiffBuilder.ComputeInlineSegments(op.SrcText!, op.TgtText!);
                        pairs.Add(SqlDiffPair.Both(
                            SqlDiffLine.Modified(op.SrcText!, srcNo++, srcSegs),
                            SqlDiffLine.Modified(op.TgtText!, tgtNo++, tgtSegs)));
                        break;
                }
            }
        }

        /// <summary>
        /// Returns the line count of a body string (for tree node labels).
        /// </summary>
        public static int LineCount ( string body ) => SplitLines(body).Count;

        // ════════════════════════════════════════════════════════════════════
        // LCS ENGINE
        // ════════════════════════════════════════════════════════════════════

        private enum OpKind { Context, Removed, Added, Modified }

        private class EditOp
        {
            public OpKind Kind { get; set; }
            public string? SrcText { get; set; }
            public string? TgtText { get; set; }
        }

        private static List<EditOp> ComputeEditScript ( List<string> srcLines, List<string> tgtLines )
        {
            var srcNorm = srcLines.Select(NormLine).ToList();
            var tgtNorm = tgtLines.Select(NormLine).ToList();

            int m = srcNorm.Count, n = tgtNorm.Count;
            var dp = new int[m + 1, n + 1];

            for ( int r = 1; r <= m; r++ )
                for ( int c = 1; c <= n; c++ )
                    dp[r, c] = srcNorm[r - 1] == tgtNorm[c - 1]
                        ? dp[r - 1, c - 1] + 1
                        : Math.Max(dp[r - 1, c], dp[r, c - 1]);

            var ops = new List<EditOp>();
            int si = m, ti = n;

            while ( si > 0 || ti > 0 )
            {
                if ( si > 0 && ti > 0 && srcNorm[si - 1] == tgtNorm[ti - 1] )
                {
                    ops.Add(new EditOp { Kind = OpKind.Context, SrcText = srcLines[si - 1], TgtText = tgtLines[ti - 1] });
                    si--; ti--;
                }
                else if ( ti > 0 && (si == 0 || dp[si, ti - 1] >= dp[si - 1, ti]) )
                {
                    ops.Add(new EditOp { Kind = OpKind.Added, TgtText = tgtLines[ti - 1] });
                    ti--;
                }
                else
                {
                    ops.Add(new EditOp { Kind = OpKind.Removed, SrcText = srcLines[si - 1] });
                    si--;
                }
            }

            ops.Reverse();
            return ops;
        }

        private static List<EditOp> MergeModifiedPairs ( List<EditOp> ops )
        {
            var result = new List<EditOp>();
            int i = 0;

            while ( i < ops.Count )
            {
                var removed = new List<EditOp>();
                while ( i < ops.Count && ops[i].Kind == OpKind.Removed )
                    removed.Add(ops[i++]);

                var added = new List<EditOp>();
                while ( i < ops.Count && ops[i].Kind == OpKind.Added )
                    added.Add(ops[i++]);

                if ( removed.Count == 0 && added.Count == 0 )
                {
                    if ( i < ops.Count ) result.Add(ops[i++]);
                    continue;
                }

                int paired = Math.Min(removed.Count, added.Count);
                for ( int p = 0; p < paired; p++ )
                {
                    var rem = removed[p].SrcText!;
                    var add = added[p].TgtText!;
                    if ( TokenOverlap(rem, add) >= ModifiedPairThreshold )
                        result.Add(new EditOp { Kind = OpKind.Modified, SrcText = rem, TgtText = add });
                    else
                    {
                        result.Add(new EditOp { Kind = OpKind.Removed, SrcText = rem });
                        result.Add(new EditOp { Kind = OpKind.Added, TgtText = add });
                    }
                }

                for ( int p = paired; p < removed.Count; p++ ) result.Add(removed[p]);
                for ( int p = paired; p < added.Count; p++ ) result.Add(added[p]);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        internal static List<string> SplitLines ( string sql ) =>
            string.IsNullOrEmpty(sql)
                ? new List<string>()
                : sql.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();

        private static string NormLine ( string line ) =>
            Regex.Replace(line.Trim(), @"\s+", " ").ToLowerInvariant();

        private static double TokenOverlap ( string a, string b )
        {
            var tA = Tokenize(a);
            var tB = Tokenize(b);
            if ( tA.Count == 0 && tB.Count == 0 ) return 1.0;
            if ( tA.Count == 0 || tB.Count == 0 ) return 0.0;
            int common = tA.Count(t => tB.Contains(t));
            int union = tA.Union(tB).Count();
            return union == 0 ? 0 : ( double ) common / union;
        }

        private static HashSet<string> Tokenize ( string line ) =>
            new(Regex.Matches(line.ToLowerInvariant(), @"[a-z0-9_]+")
                     .Select(m => m.Value), StringComparer.Ordinal);
    }
}