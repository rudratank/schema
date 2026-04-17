using DbForge.Core.Compare;
using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Builds side-by-side diff pairs and the object tree for a pair of views.
    ///
    /// Sections emitted
    /// ─────────────────
    ///   ── View Info ──    IsSchemaBound / IsIndexed flags
    ///   ── Body ──         LCS line-by-line diff with inline token highlights
    /// </summary>
    public static class ViewDiffBuilder
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            ViewDefinition? src,
            ViewDefinition? tgt )
        {
            var pairs = new List<SqlDiffPair>();

            if ( src == null && tgt == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No view selected"),
                    SqlDiffLine.Context("-- No view selected")));
                return pairs;
            }

            if ( src == null )
            {
                pairs.Add(SqlDiffPair.Header("── View (Only in Target) ──────────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(tgt!.Definition) )
                    pairs.Add(SqlDiffPair.TargetOnly(line));
                return pairs;
            }

            if ( tgt == null )
            {
                pairs.Add(SqlDiffPair.Header("── View (Only in Source) ──────────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(src.Definition) )
                    pairs.Add(SqlDiffPair.SourceOnly(line));
                return pairs;
            }

            // ── View Info ────────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── View Info ──────────────────────────────────────────────────"));
            AppendFlagLine(pairs, "SCHEMABINDING", src.IsSchemaBound, tgt.IsSchemaBound);
            AppendFlagLine(pairs, "Indexed (clustered)", src.IsIndexed, tgt.IsIndexed);

            // ── Body ─────────────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Body ───────────────────────────────────────────────────────"));
            BodyDiffEngine.AppendBodyDiff(
                pairs,
                ViewComparer.ExtractBody(src.Definition),
                ViewComparer.ExtractBody(tgt.Definition));

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            ViewDefinition? src,
            ViewDefinition? tgt,
            string viewName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            string status = src == null ? "Added"
                          : tgt == null ? "Removed"
                          : ViewComparer.NormalizeFull(src.Definition) ==
                            ViewComparer.NormalizeFull(tgt.Definition) ? "Identical"
                          : "Modified";

            var node = new DiffTreeNode
            {
                Label = viewName,
                Icon = "◫",
                NodeKind = "View",
                DiffStatus = status,
                IsExpanded = true
            };

            if ( src != null && tgt != null )
            {
                // Properties sub-node
                bool propsChanged = src.IsSchemaBound != tgt.IsSchemaBound ||
                                    src.IsIndexed != tgt.IsIndexed;
                node.Children.Add(new DiffTreeNode
                {
                    Label = "Properties",
                    Icon = "◈",
                    NodeKind = "Info",
                    DiffStatus = propsChanged ? "Modified" : "Identical"
                });

                // Body sub-node
                var srcBody = ViewComparer.ExtractBody(src.Definition);
                var tgtBody = ViewComparer.ExtractBody(tgt.Definition);
                var bodyStatus = ViewComparer.NormalizeFull(srcBody) ==
                                 ViewComparer.NormalizeFull(tgtBody) ? "Identical" : "Modified";

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
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static void AppendFlagLine (
            List<SqlDiffPair> pairs, string label, bool srcVal, bool tgtVal )
        {
            string srcText = $"    -- {label}: {srcVal}";
            string tgtText = $"    -- {label}: {tgtVal}";

            if ( srcVal == tgtVal )
                pairs.Add(SqlDiffPair.Unchanged(srcText, 0, 0));
            else
            {
                var (ss, ts) = SqlDiffBuilder.ComputeInlineSegments(srcText, tgtText);
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Modified(srcText, segments: ss),
                    SqlDiffLine.Modified(tgtText, segments: ts)));
            }
        }
    }
}