using DbForge.Core.Models;
using DbForge.Infrastructure.Persistence;

namespace DbForge.WPF.Services
{
    /// <summary>
    /// SQLite-based implementation of IAppSettings using AppDbContext.
    /// Stores settings in a simple key-value table.
    /// </summary>
    public class AppSettingsService : IAppSettings
    {
        private readonly AppDbContext _dbContext;
        private readonly Dictionary<string, string> _cache = new();
        private bool _isLoaded;

        public AppSettingsService ( AppDbContext dbContext )
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Load settings from database into memory cache (one-time operation).
        /// </summary>
        private void EnsureLoaded ()
        {
            if ( _isLoaded ) return;

            // Assuming there's an AppSetting entity in your DbContext
            // If not, you may need to add it or adapt to your existing schema
            var settings = _dbContext.AppSettings?.ToList() ?? new();
            _cache.Clear();
            foreach ( var setting in settings )
                _cache[setting.Key] = setting.Value ?? string.Empty;

            _isLoaded = true;
        }

        public string GetSetting ( string key, string? defaultValue = null )
        {
            EnsureLoaded();
            return _cache.TryGetValue(key, out var value) ? value : (defaultValue ?? string.Empty);
        }

        public void SetSetting ( string key, string value )
        {
            EnsureLoaded();
            _cache[key] = value;

            var existing = _dbContext.AppSettings?.FirstOrDefault(s => s.Key == key);
            if ( existing != null )
            {
                existing.Value = value;
                _dbContext.AppSettings?.Update(existing);
            }
            else
            {
                var newSetting = new AppSetting { Key = key, Value = value };
                _dbContext.AppSettings?.Add(newSetting);
            }

            _dbContext.SaveChanges();
        }

        public bool HasSetting ( string key )
        {
            EnsureLoaded();
            return _cache.ContainsKey(key);
        }

        public void RemoveSetting ( string key )
        {
            EnsureLoaded();
            _cache.Remove(key);

            var existing = _dbContext.AppSettings?.FirstOrDefault(s => s.Key == key);
            if ( existing != null )
            {
                _dbContext.AppSettings?.Remove(existing);
                _dbContext.SaveChanges();
            }
        }

        public void ClearAll ()
        {
            _cache.Clear();
            _dbContext.AppSettings?.RemoveRange(_dbContext.AppSettings);
            _dbContext.SaveChanges();
        }

        public Dictionary<string, string> GetAllSettings ()
        {
            EnsureLoaded();
            return new Dictionary<string, string>(_cache);
        }

        /// <summary>
        /// Refresh cache from database (useful if settings changed externally).
        /// </summary>
        public void Refresh ()
        {
            _isLoaded = false;
            EnsureLoaded();
        }
    }

    /// <summary>
    /// Entity model for application settings (add this to your AppDbContext).
    /// </summary>

}