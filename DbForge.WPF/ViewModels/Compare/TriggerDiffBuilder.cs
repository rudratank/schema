using DbForge.Core.Compare;
using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Builds side-by-side diff pairs and the object tree for a pair of triggers.
    ///
    /// Sections emitted
    /// ─────────────────
    ///   ── Trigger Info ──  parent table, timing (AFTER/INSTEAD OF), events, enabled
    ///   ── Body ──          LCS line-by-line diff with inline token highlights
    /// </summary>
    public static class TriggerDiffBuilder
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            TriggerDefinition? src,
            TriggerDefinition? tgt )
        {
            var pairs = new List<SqlDiffPair>();

            if ( src == null && tgt == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No trigger selected"),
                    SqlDiffLine.Context("-- No trigger selected")));
                return pairs;
            }

            if ( src == null )
            {
                pairs.Add(SqlDiffPair.Header("── Trigger (Only in Target) ───────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(tgt!.Definition) )
                    pairs.Add(SqlDiffPair.TargetOnly(line));
                return pairs;
            }

            if ( tgt == null )
            {
                pairs.Add(SqlDiffPair.Header("── Trigger (Only in Source) ───────────────────────────────────"));
                foreach ( var line in BodyDiffEngine.SplitLines(src.Definition) )
                    pairs.Add(SqlDiffPair.SourceOnly(line));
                return pairs;
            }

            // ── Trigger Info ──────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Trigger Info ───────────────────────────────────────────────"));
            AppendInfoLine(pairs, "Parent table", src.ParentTable, tgt.ParentTable);
            AppendInfoLine(pairs, "Timing", src.Timing.ToString(), tgt.Timing.ToString());
            AppendInfoLine(pairs, "Events", FormatEvents(src.Events), FormatEvents(tgt.Events));
            AppendInfoLine(pairs, "Enabled", src.IsEnabled.ToString(), tgt.IsEnabled.ToString());

            // ── Body ──────────────────────────────────────────────────────────
            pairs.Add(SqlDiffPair.Header("── Body ───────────────────────────────────────────────────────"));
            BodyDiffEngine.AppendBodyDiff(
                pairs,
                TriggerComparer.ExtractBody(src.Definition),
                TriggerComparer.ExtractBody(tgt.Definition));

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            TriggerDefinition? src,
            TriggerDefinition? tgt,
            string triggerName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            string status = src == null ? "Added"
                          : tgt == null ? "Removed"
                          : TriggerComparer.NormalizeFull(src.Definition) ==
                            TriggerComparer.NormalizeFull(tgt.Definition)
                            && src.Events == tgt.Events
                            && src.Timing == tgt.Timing
                            && src.IsEnabled == tgt.IsEnabled
                              ? "Identical"
                              : "Modified";

            var node = new DiffTreeNode
            {
                Label = triggerName,
                Icon = "⚡",
                NodeKind = "Trigger",
                DiffStatus = status,
                IsExpanded = true
            };

            if ( src != null && tgt != null )
            {
                bool infoChanged = src.Events != tgt.Events
                                || src.Timing != tgt.Timing
                                || src.IsEnabled != tgt.IsEnabled
                                || !string.Equals(src.ParentTable, tgt.ParentTable,
                                                  StringComparison.OrdinalIgnoreCase);

                node.Children.Add(new DiffTreeNode
                {
                    Label = $"Info  ({FormatEvents(src.Events)}, {src.Timing})",
                    Icon = "◈",
                    NodeKind = "Info",
                    DiffStatus = infoChanged ? "Modified" : "Identical"
                });

                var srcBody = TriggerComparer.ExtractBody(src.Definition);
                var tgtBody = TriggerComparer.ExtractBody(tgt.Definition);
                var bodyStatus = TriggerComparer.NormalizeFull(srcBody) ==
                                 TriggerComparer.NormalizeFull(tgtBody) ? "Identical" : "Modified";

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

        private static string FormatEvents ( TriggerEvents e )
        {
            var parts = new List<string>();
            if ( e.HasFlag(TriggerEvents.Insert) ) parts.Add("INSERT");
            if ( e.HasFlag(TriggerEvents.Update) ) parts.Add("UPDATE");
            if ( e.HasFlag(TriggerEvents.Delete) ) parts.Add("DELETE");
            return parts.Count > 0 ? string.Join(", ", parts) : "NONE";
        }
    }
}