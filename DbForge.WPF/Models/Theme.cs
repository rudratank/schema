namespace DbForge.WPF.Models
{
    /// <summary>
    /// Represents a single theme with its metadata and resource dictionary URI.
    /// </summary>
    public class Theme
    {
        public Theme ( string id, string name, string description, string resourceUri, bool isDark = false )
        {
            Id = id;
            Name = name;
            Description = description;
            ResourceUri = resourceUri;
            IsDark = isDark;
        }

        /// <summary>
        /// Unique identifier (e.g., "light", "dark", "ocean", "forest").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name shown in UI (e.g., "Light Theme").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description for tooltips or settings (e.g., "Clean light theme for daytime use").
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// URI to the theme's ResourceDictionary (e.g., "pack://application:,,,/UI/Themes/Light.xaml").
        /// </summary>
        public string ResourceUri { get; }

        /// <summary>
        /// Whether this is a dark theme (affects window chrome, system decorations, etc.).
        /// </summary>
        public bool IsDark { get; }

        public override string ToString () => Name;
    }
}