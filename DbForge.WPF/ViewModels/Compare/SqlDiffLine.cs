namespace DbForge.WPF.ViewModels.Compare
{
    public class SqlDiffLine
    {
        /// <summary>Added | Removed | Modified | Context | Empty | SectionHeader</summary>
        public string LineKind { get; init; } = "Context";

        /// <summary>Full text of the line (kept for copy commands and fallback).</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>1-based line number, 0 for placeholder / header lines.</summary>
        public int LineNumber { get; init; }

        /// <summary>
        /// Token-level inline segments.  Always non-null.
        /// Modified lines may contain highlighted segments; every other kind has
        /// exactly one non-highlighted segment (or zero for Empty lines).
        /// TextBlockHelper renders these as Inlines so only changed tokens
        /// receive a background — text colour is never touched here.
        /// </summary>
        public IReadOnlyList<DiffSegment> Segments { get; init; } = Array.Empty<DiffSegment>();

        // ── Convenience factories ─────────────────────────────────────────────

        public static SqlDiffLine Added ( string text, int lineNo = 0 ) => new()
        {
            LineKind = "Added",
            Text = text,
            LineNumber = lineNo,
            // ✅ IsHighlighted=true → TextBlockHelper renders a green token-background block,
            //    not green text. Text colour stays FgCode.
            Segments = new[] { new DiffSegment { Text = text, IsHighlighted = true } }
        };

        public static SqlDiffLine Removed ( string text, int lineNo = 0 ) => new()
        {
            LineKind = "Removed",
            Text = text,
            LineNumber = lineNo,
            // ✅ IsHighlighted=true → amber token-background block on source pane.
            Segments = new[] { new DiffSegment { Text = text, IsHighlighted = true } }
        };

        /// <summary>
        /// A modified line.  Pass pre-computed <paramref name="segments"/> from
        /// <c>SqlDiffBuilder.ComputeInlineSegments</c> to get token highlights;
        /// omit / null for a plain single-segment fallback.
        /// </summary>
        public static SqlDiffLine Modified ( string text, int lineNo = 0,
                                           IReadOnlyList<DiffSegment>? segments = null ) => new()
                                           {
                                               LineKind = "Modified",
                                               Text = text,
                                               LineNumber = lineNo,
                                               Segments = segments ?? new[] { new DiffSegment { Text = text } }
                                           };

        public static SqlDiffLine Context ( string text, int lineNo = 0 ) => new()
        {
            LineKind = "Context",
            Text = text,
            LineNumber = lineNo,
            Segments = new[] { new DiffSegment { Text = text } }
        };

        /// <summary>Blank placeholder that keeps the two panes vertically aligned.</summary>
        public static SqlDiffLine Empty () => new()
        {
            LineKind = "Empty",
            Text = string.Empty,
            LineNumber = 0,
            Segments = Array.Empty<DiffSegment>()
        };

        /// <summary>Section separator shown in both panes (e.g. "── Columns ──").</summary>
        public static SqlDiffLine SectionHeader ( string label ) => new()
        {
            LineKind = "SectionHeader",
            Text = label,
            LineNumber = 0,
            Segments = new[] { new DiffSegment { Text = label } }
        };
    }
}