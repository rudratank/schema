namespace DbForge.WPF.Services
{
    /// <summary>
    /// Interface for persistent application settings storage.
    /// Implementations can use SQLite, JSON files, or other storage mechanisms.
    /// </summary>
    public interface IAppSettings
    {
        /// <summary>
        /// Get a setting value by key, with an optional default value.
        /// </summary>
        string GetSetting ( string key, string? defaultValue = null );

        /// <summary>
        /// Set a setting value by key.
        /// </summary>
        void SetSetting ( string key, string value );

        /// <summary>
        /// Check if a setting exists.
        /// </summary>
        bool HasSetting ( string key );

        /// <summary>
        /// Remove a setting by key.
        /// </summary>
        void RemoveSetting ( string key );

        /// <summary>
        /// Clear all settings.
        /// </summary>
        void ClearAll ();

        /// <summary>
        /// Get all settings as a dictionary.
        /// </summary>
        Dictionary<string, string> GetAllSettings ();
    }
}