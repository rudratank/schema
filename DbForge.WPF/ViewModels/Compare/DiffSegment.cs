namespace DbForge.WPF.ViewModels.Compare
{
    public sealed class DiffSegment
    {
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// True  → this token was changed (show inline background highlight).
        /// False → unchanged context text.
        /// </summary>
        public bool IsHighlighted { get; init; }
    }
}
