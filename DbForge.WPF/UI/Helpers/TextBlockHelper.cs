using DbForge.WPF.ViewModels.Compare;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DbForge.WPF.UI.Helpers
{

    public static class TextBlockHelper
    {
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

            // Source highlighted tokens: deep amber/red  → "this was removed here"
            // Target highlighted tokens: deep green      → "this is what replaced it"
            var kind = GetHighlightKind(tb);
            var hlBrush = kind == "Target"
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x4A, 0x22))  // #1A4A22 deep green
                : new SolidColorBrush(Color.FromRgb(0x5C, 0x28, 0x00));  // #5C2800 deep amber

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