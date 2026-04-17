using DbForge.Core.Models.Schema;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    /// <summary>
    /// Builds side-by-side diff pairs and the object tree for a pair of synonyms.
    ///
    /// Synonyms are trivial — there is no body, only a BaseObjectName.
    /// The diff pane shows a simple two-row comparison of the target reference.
    /// </summary>
    public static class SynonymDiffBuilder
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public static List<SqlDiffPair> BuildDiffPairs (
            SynonymDefinition? src,
            SynonymDefinition? tgt )
        {
            var pairs = new List<SqlDiffPair>();

            pairs.Add(SqlDiffPair.Header("── Synonym Definition ─────────────────────────────────────────"));

            if ( src == null && tgt == null )
            {
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Context("-- No synonym selected"),
                    SqlDiffLine.Context("-- No synonym selected")));
                return pairs;
            }

            if ( src == null )
            {
                pairs.Add(SqlDiffPair.TargetOnly(FormatSynonym(tgt!)));
                return pairs;
            }

            if ( tgt == null )
            {
                pairs.Add(SqlDiffPair.SourceOnly(FormatSynonym(src)));
                return pairs;
            }

            string srcText = FormatSynonym(src);
            string tgtText = FormatSynonym(tgt);

            if ( srcText == tgtText )
            {
                pairs.Add(SqlDiffPair.Unchanged(srcText, 0, 0));
            }
            else
            {
                // Inline token diff so the changed part of the base object name is highlighted
                var (ss, ts) = SqlDiffBuilder.ComputeInlineSegments(srcText, tgtText);
                pairs.Add(SqlDiffPair.Both(
                    SqlDiffLine.Modified(srcText, segments: ss),
                    SqlDiffLine.Modified(tgtText, segments: ts)));
            }

            return pairs;
        }

        public static ObservableCollection<DiffTreeNode> BuildTree (
            SynonymDefinition? src,
            SynonymDefinition? tgt,
            string synonymName )
        {
            var root = new ObservableCollection<DiffTreeNode>();

            string status = src == null ? "Added"
                          : tgt == null ? "Removed"
                          : NormalizeObjectName(src.BaseObjectName) ==
                            NormalizeObjectName(tgt.BaseObjectName) ? "Identical"
                          : "Modified";

            var node = new DiffTreeNode
            {
                Label = synonymName,
                Icon = "↔",
                NodeKind = "Synonym",
                DiffStatus = status,
                IsExpanded = true
            };

            if ( src != null && tgt != null )
            {
                bool changed = NormalizeObjectName(src.BaseObjectName) !=
                               NormalizeObjectName(tgt.BaseObjectName);
                node.Children.Add(new DiffTreeNode
                {
                    Label = $"Points to: {src.BaseObjectName}",
                    Icon = "▪",
                    NodeKind = "Info",
                    DiffStatus = changed ? "Modified" : "Identical"
                });
            }

            root.Add(node);
            return root;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static string FormatSynonym ( SynonymDefinition s ) =>
            $"    CREATE SYNONYM [{s.SchemaName}].[{s.Name}] FOR {s.BaseObjectName}";

        private static string NormalizeObjectName ( string name ) =>
            name.Replace("[", "").Replace("]", "").Trim().ToLowerInvariant();
    }
}