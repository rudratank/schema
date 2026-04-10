namespace DbForge.WPF.ViewModels.Compare
{
    public class SqlDiffPair
    {
        public SqlDiffLine Source { get; init; } = SqlDiffLine.Empty();
        public SqlDiffLine Target { get; init; } = SqlDiffLine.Empty();

        // ── Convenience factories ───────────────────────────────────────────────

        public static SqlDiffPair Both ( SqlDiffLine src, SqlDiffLine tgt ) =>
            new() { Source = src, Target = tgt };

        public static SqlDiffPair SourceOnly ( string text, int lineNo = 0 ) =>
            new() { Source = SqlDiffLine.Removed(text, lineNo), Target = SqlDiffLine.Empty() };

        public static SqlDiffPair TargetOnly ( string text, int lineNo = 0 ) =>
            new() { Source = SqlDiffLine.Empty(), Target = SqlDiffLine.Added(text, lineNo) };

        public static SqlDiffPair Unchanged ( string text, int srcLine, int tgtLine ) =>
            new()
            {
                Source = SqlDiffLine.Context(text, srcLine),
                Target = SqlDiffLine.Context(text, tgtLine)
            };

        public static SqlDiffPair Header ( string label ) =>
            new()
            {
                Source = SqlDiffLine.SectionHeader(label),
                Target = SqlDiffLine.SectionHeader(label)
            };
    }
}
