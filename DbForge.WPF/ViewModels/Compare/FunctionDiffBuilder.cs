using DbForge.Core.Compare;
using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Builds side-by-side diff pairs and the object tree for a pair of functions.
    ///
    /// Sections emitted
    /// ─────────────────
    ///   ── Function Info ──   type (Scalar / InlineTVF / MultiTVF) + return type
    ///   ── Parameters ──      per-param diff (same logic as ProcedureDiffBuilder)
    ///   ── Body ──            LCS line-by-line diff with inline token highlights
    /// </summary>
    public static class FunctionDiffBuilder
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            FunctionDefinition? src,
            FunctionDefinition? tgt )
        {
            var pairs = new List<SqlDiffPair>();

            if ( src == null && tgt == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No function selected"),
                    SqlDiffLine.Context("-- No function selected")));
                return pairs;
            }

            if ( src == null )
            {
                pairs.Add(SqlDiffPair.Header("── Function (Only in Target) ──────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(tgt!.Definition) )
                    pairs.Add(SqlDiffPair.TargetOnly(line));
                return pairs;
            }

            if ( tgt == null )
            {
                pairs.Add(SqlDiffPair.Header("── Function (Only in Source) ──────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(src.Definition) )
                    pairs.Add(SqlDiffPair.SourceOnly(line));
                return pairs;
            }

            // ── Function Info ─────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Function Info ──────────────────────────────────────────────"));
            AppendInfoLine(pairs, "Type", src.FunctionType.ToString(), tgt.FunctionType.ToString());
            AppendInfoLine(pairs, "Returns", src.ReturnType, tgt.ReturnType);

            // ── Parameters ───────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Parameters ─────────────────────────────────────────────────"));
            BuildParameterLines(pairs, src, tgt);

            // ── Body ──────────────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Body ───────────────────────────────────────────────────────"));
            BodyDiffEngine.AppendBodyDiff(
                pairs,
                FunctionComparer.ExtractBody(src.Definition),
                FunctionComparer.ExtractBody(tgt.Definition));

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            FunctionDefinition? src,
            FunctionDefinition? tgt,
            string fnName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            string status = src == null ? "Added"
                          : tgt == null ? "Removed"
                          : FunctionComparer.NormalizeFull(src.Definition) ==
                            FunctionComparer.NormalizeFull(tgt.Definition) ? "Identical"
                          : "Modified";

            var node = new DiffTreeNode
            {
                Label = fnName,
                Icon = "ƒ",
                NodeKind = "Function",
                DiffStatus = status,
                IsExpanded = true
            };

            if ( src != null && tgt != null )
            {
                // Info
                bool infoChanged = src.FunctionType != tgt.FunctionType ||
                                   !string.Equals(src.ReturnType, tgt.ReturnType,
                                                  StringComparison.OrdinalIgnoreCase);
                node.Children.Add(new DiffTreeNode
                {
                    Label = "Function Info",
                    Icon = "◈",
                    NodeKind = "Info",
                    DiffStatus = infoChanged ? "Modified" : "Identical"
                });

                // Parameters
                var srcParams = ProcedureComparer.ExtractParameters(src.Definition);
                var tgtParams = ProcedureComparer.ExtractParameters(tgt.Definition);
                var paramNode = BuildParamTreeSection(srcParams, tgtParams);
                node.Children.Add(paramNode);

                // Body
                var srcBody = FunctionComparer.ExtractBody(src.Definition);
                var tgtBody = FunctionComparer.ExtractBody(tgt.Definition);
                var bodyStatus = FunctionComparer.NormalizeFull(srcBody) ==
                                 FunctionComparer.NormalizeFull(tgtBody) ? "Identical" : "Modified";

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
        // PARAMETER SECTION  (same display logic as ProcedureDiffBuilder)
        // ════════════════════════════════════════════════════════════════════

        private static void BuildParameterLines (
            List<SqlDiffPair> pairs,
            FunctionDefinition src,
            FunctionDefinition tgt )
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

            var allNames = srcParams.Select(p => p.Name)
                .Concat(tgtParams.Select(p => p.Name).Where(n => !srcMap.ContainsKey(n)))
                .ToList();

            foreach ( var name in allNames )
            {
                bool inSrc = srcMap.TryGetValue(name, out var sp);
                bool inTgt = tgtMap.TryGetValue(name, out var tp);
                string srcT = inSrc ? FormatParam(sp!) : string.Empty;
                string tgtT = inTgt ? FormatParam(tp!) : string.Empty;

                if ( inSrc && inTgt )
                {
                    if ( srcT == tgtT )
                        pairs.Add(SqlDiffPair.Unchanged(srcT, 0, 0));
                    else
                    {
                        var (ss, ts) = SqlDiffBuilder.ComputeInlineSegments(srcT, tgtT);
                        pairs.Add(SqlDiffPair.Both(
                            SqlDiffLine.Modified(srcT, segments: ss),
                            SqlDiffLine.Modified(tgtT, segments: ts)));
                    }
                }
                else if ( inSrc )
                    pairs.Add(SqlDiffPair.SourceOnly(srcT));
                else
                    pairs.Add(SqlDiffPair.TargetOnly(tgtT));
            }
        }

        private static DiffTreeNode BuildParamTreeSection (
            List<ProcParam> srcParams, List<ProcParam> tgtParams )
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

        private static void AppendInfoLine (
            List<SqlDiffPair> pairs, string label, string srcVal, string tgtVal )
        {
            string srcText = $"    -- {label}: {srcVal}";
            string tgtText = $"    -- {label}: {tgtVal}";

            if ( string.Equals(srcVal, tgtVal, StringComparison.OrdinalIgnoreCase) )
                pairs.Add(SqlDiffPair.Unchanged(srcText, 0, 0));
            else
            {
                var (ss, ts) = SqlDiffBuilder.ComputeInlineSegments(srcText, tgtText);
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Modified(srcText, segments: ss),
                    SqlDiffLine.Modified(tgtText, segments: ts)));
            }
        }

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