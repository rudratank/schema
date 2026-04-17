using DbForge.Core.Compare;
using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Builds side-by-side diff pairs and the object tree for a pair of stored procedures.
    ///
    /// Sections emitted
    /// ─────────────────
    ///   ── Parameters ──   per-param diff (added / removed / type changed / default changed)
    ///   ── Body ──         LCS line-by-line diff via shared BodyDiffEngine
    ///
    /// The LCS + Modified-pair merge algorithm lives in BodyDiffEngine and is
    /// shared with ViewDiffBuilder, FunctionDiffBuilder, and TriggerDiffBuilder.
    /// This file no longer contains its own copy of the algorithm.
    /// </summary>
    public static class ProcedureDiffBuilder
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            ProcedureDefinition? src,
            ProcedureDefinition? tgt )
        {
            var pairs = new List<SqlDiffPair>();

            if ( src == null && tgt == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No procedure selected"),
                    SqlDiffLine.Context("-- No procedure selected")));
                return pairs;
            }

            if ( src == null )
            {
                pairs.Add(SqlDiffPair.Header("── Procedure (Only in Target) ─────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(tgt!.Definition) )
                    pairs.Add(SqlDiffPair.TargetOnly(line));
                return pairs;
            }

            if ( tgt == null )
            {
                pairs.Add(SqlDiffPair.Header("── Procedure (Only in Source) ─────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(src.Definition) )
                    pairs.Add(SqlDiffPair.SourceOnly(line));
                return pairs;
            }

            // ── Parameters ───────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Parameters ─────────────────────────────────────────────────"));
            BuildParameterLines(pairs, src, tgt);

            // ── Body ─────────────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Body ───────────────────────────────────────────────────────"));
            BodyDiffEngine.AppendBodyDiff(
                pairs,
                ProcedureComparer.ExtractBody(src.Definition),
                ProcedureComparer.ExtractBody(tgt.Definition));

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            ProcedureDefinition? src,
            ProcedureDefinition? tgt,
            string procName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            string status = src == null ? "Added"
                          : tgt == null ? "Removed"
                          : ProcedureComparer.NormalizeFull(src.Definition) ==
                            ProcedureComparer.NormalizeFull(tgt.Definition) ? "Identical"
                          : "Modified";

            var node = new DiffTreeNode
            {
                Label = procName,
                Icon = "⚙",
                NodeKind = "Procedure",
                DiffStatus = status,
                IsExpanded = true
            };

            if ( src != null && tgt != null )
            {
                var srcParams = ProcedureComparer.ExtractParameters(src.Definition);
                var tgtParams = ProcedureComparer.ExtractParameters(tgt.Definition);
                node.Children.Add(BuildParamTreeSection(srcParams, tgtParams));

                var srcBody = ProcedureComparer.ExtractBody(src.Definition);
                var tgtBody = ProcedureComparer.ExtractBody(tgt.Definition);
                var bodyStatus = ProcedureComparer.NormalizeFull(srcBody) ==
                                 ProcedureComparer.NormalizeFull(tgtBody)
                                 ? "Identical" : "Modified";

                node.Children.Add(new DiffTreeNode
                {
                    Label = $"Body  ({BodyDiffEngine.LineCount(srcBody)} → {BodyDiffEngine.LineCount(tgtBody)} lines)",
                    Icon = "≡",
                    NodeKind = "Info",
                    DiffStatus = bodyStatus
                });
            }

            root.Add(node);
            return root;
        }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETER SECTION
        // ════════════════════════════════════════════════════════════════════

        private static void BuildParameterLines (
            List<SqlDiffPair> pairs,
            ProcedureDefinition src,
            ProcedureDefinition tgt )
        {
            var srcParams = ProcedureComparer.ExtractParameters(src.Definition);
            var tgtParams = ProcedureComparer.ExtractParameters(tgt.Definition);

            if ( srcParams.Count == 0 && tgtParams.Count == 0 )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("    -- No parameters"),
                    SqlDiffLine.Context("    -- No parameters")));
                return;
            }

            var srcMap = srcParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var tgtMap = tgtParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            // Source-order first, then target-only extras
            var allNames = srcParams.Select(p => p.Name)
                .Concat(tgtParams.Select(p => p.Name).Where(n => !srcMap.ContainsKey(n)))
                .ToList();

            foreach ( var name in allNames )
            {
                bool inSrc = srcMap.TryGetValue(name, out var sp);
                bool inTgt = tgtMap.TryGetValue(name, out var tp);

                string srcText = inSrc ? FormatParam(sp!) : string.Empty;
                string tgtText = inTgt ? FormatParam(tp!) : string.Empty;

                if ( inSrc && inTgt )
                {
                    if ( srcText == tgtText )
                    {
                        pairs.Add(SqlDiffPair.Unchanged(srcText, 0, 0));
                    }
                    else
                    {
                        var (srcSegs, tgtSegs) = SqlDiffBuilder.ComputeInlineSegments(srcText, tgtText);
                        pairs.Add(SqlDiffPair.Both(
                            SqlDiffLine.Modified(srcText, segments: srcSegs),
                            SqlDiffLine.Modified(tgtText, segments: tgtSegs)));
                    }
                }
                else if ( inSrc )
                {
                    pairs.Add(SqlDiffPair.SourceOnly(srcText));
                }
                else
                {
                    pairs.Add(SqlDiffPair.TargetOnly(tgtText));
                }
            }
        }

        private static DiffTreeNode BuildParamTreeSection (
            List<ProcParam> srcParams,
            List<ProcParam> tgtParams )
        {
            var srcMap = srcParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var tgtMap = tgtParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            bool anyDiff =
                srcParams.Any(p => !tgtMap.ContainsKey(p.Name)) ||
                tgtParams.Any(p => !srcMap.ContainsKey(p.Name)) ||
                srcParams.Any(p =>
                    tgtMap.TryGetValue(p.Name, out var tp) &&
                    FormatParam(p) != FormatParam(tp));

            var section = new DiffTreeNode
            {
                Label = "Parameters",
                Icon = "◈",
                NodeKind = "Parameters",
                DiffStatus = anyDiff ? "Modified" : "Identical",
                IsExpanded = true
            };

            var allNames = srcParams.Select(p => p.Name)
                .Concat(tgtParams.Select(p => p.Name).Where(n => !srcMap.ContainsKey(n)));

            foreach ( var name in allNames )
            {
                bool inSrc = srcMap.TryGetValue(name, out var sp);
                bool inTgt = tgtMap.TryGetValue(name, out var tp);

                string nodeStatus = !inSrc ? "Added"
                                  : !inTgt ? "Removed"
                                  : FormatParam(sp!) != FormatParam(tp!) ? "Modified"
                                  : "Identical";

                string label = inSrc
                    ? $"{name}  ({sp!.DataType}{(sp.IsOutput ? ", OUTPUT" : "")})"
                    : $"{name}  ({tp!.DataType}{(tp.IsOutput ? ", OUTPUT" : "")})";

                section.Children.Add(new DiffTreeNode
                {
                    Label = label,
                    Icon = "▪",
                    NodeKind = "Parameter",
                    DiffStatus = nodeStatus
                });
            }

            return section;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static string FormatParam ( ProcParam p )
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"    {p.Name}  {p.DataType}");
            if ( p.DefaultValue != null ) sb.Append($" = {p.DefaultValue}");
            if ( p.IsOutput ) sb.Append(" OUTPUT");
            return sb.ToString();
        }
    }
}