using DbForge.WPF.ViewModels.Compare;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DbForge.WPF.UI.Helpers
{
    /// <summary>
    /// Attached behaviour that renders a list of <see cref="DiffSegment"/> objects
    /// as inline Runs inside a TextBlock, with optional background highlights for
    /// changed tokens.
    ///
    /// Usage in XAML
    /// ─────────────
    ///   helpers:TextBlockHelper.HighlightKind="Source"
    ///   helpers:TextBlockHelper.Segments="{Binding Source.Segments}"
    ///
    /// Source → amber/red background for removed tokens.
    /// Target → green background for added tokens.
    /// </summary>
    public static class TextBlockHelper
    {
        // ── Static brushes — allocated once, not per-rebuild ──────────────────
        // Delete (Source)

        private static readonly SolidColorBrush SourceHighlightBrush =
            Frozen(new SolidColorBrush(Color.FromArgb(90, 255, 80, 80)));

        // Add (Target)
        private static readonly SolidColorBrush TargetHighlightBrush =
            Frozen(new SolidColorBrush(Color.FromArgb(90, 80, 255, 120))); // deep green

        private static SolidColorBrush Frozen ( SolidColorBrush b ) { b.Freeze(); return b; }

        // ── "Source" or "Target" ─────────────────────────────────────────────
        public static readonly DependencyProperty HighlightKindProperty =
            DependencyProperty.RegisterAttached(
                "HighlightKind",
                typeof(string),
                typeof(TextBlockHelper),
                new PropertyMetadata("Source", Rebuild));

        public static void SetHighlightKind ( TextBlock tb, string value )
            => tb.SetValue(HighlightKindProperty, value);

        public static string GetHighlightKind ( TextBlock tb )
            => ( string ) tb.GetValue(HighlightKindProperty);

        // ── Segment list ─────────────────────────────────────────────────────
        public static readonly DependencyProperty SegmentsProperty =
            DependencyProperty.RegisterAttached(
                "Segments",
                typeof(IReadOnlyList<DiffSegment>),
                typeof(TextBlockHelper),
                new PropertyMetadata(null, Rebuild));

        public static void SetSegments ( TextBlock tb, IReadOnlyList<DiffSegment> value )
            => tb.SetValue(SegmentsProperty, value);

        public static IReadOnlyList<DiffSegment>? GetSegments ( TextBlock tb )
            => ( IReadOnlyList<DiffSegment>? ) tb.GetValue(SegmentsProperty);

        // ── Rebuild Inlines ──────────────────────────────────────────────────
        private static void Rebuild ( DependencyObject d, DependencyPropertyChangedEventArgs _ )
        {
            if ( d is not TextBlock tb ) return;

            tb.Inlines.Clear();

            var segs = GetSegments(tb);
            if ( segs == null || segs.Count == 0 ) return;

            var hlBrush = GetHighlightKind(tb) == "Target"
                ? TargetHighlightBrush
                : SourceHighlightBrush;

            foreach ( var seg in segs )
            {
                var run = new Run(seg.Text);
                if ( seg.IsHighlighted )
                    run.Background = hlBrush;
                tb.Inlines.Add(run);
            }
        }
    }
}