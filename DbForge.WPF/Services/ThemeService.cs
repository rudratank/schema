using DbForge.WPF.Models;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DbForge.WPF.Services
{
    /// <summary>
    /// Manages theme switching and persistence.
    ///
    /// Architecture notes
    /// ──────────────────
    /// • A "theme" is a ResourceDictionary at /UI/Themes/{Id}.xaml that
    ///   defines every brush/color/scalar key the app uses.
    /// • GlobalStyles.xaml MUST be merged AFTER the theme dict so its
    ///   DynamicResource references resolve correctly.
    /// • ThemeService owns both merges: it removes old theme + GlobalStyles,
    ///   adds the new theme, then re-adds GlobalStyles on top.
    /// • IsDark themes also push DWMWA_USE_IMMERSIVE_DARK_MODE so the WPF
    ///   title bar matches (Windows 10 1903+ / Windows 11).
    /// </summary>
    public class ThemeService
    {
        private const string ThemesPathPrefix = "/UI/Themes/";
        private const string GlobalStylesUri = "pack://application:,,,/UI/Themes/GlobalStyles.xaml";

        private readonly IAppSettings _settings;
        private Theme _currentTheme = null!;

        public event EventHandler<Theme>? ThemeChanged;

        public IReadOnlyList<Theme> AvailableThemes { get; }
        public Theme CurrentTheme => _currentTheme;

        public ThemeService ( IAppSettings settings )
        {
            _settings = settings;

            AvailableThemes = new List<Theme>
            {
                new("dark",          "Dark",          "Professional dark theme",                          $"pack://application:,,,{ThemesPathPrefix}Dark.xaml",          isDark: true),
                new("light",         "Light",         "Clean light theme for daytime use",                $"pack://application:,,,{ThemesPathPrefix}Light.xaml",         isDark: false),
                new("ocean",         "Ocean",         "Deep blue tones inspired by the sea",              $"pack://application:,,,{ThemesPathPrefix}Ocean.xaml",         isDark: true),
                new("forest",        "Forest",        "Warm earthy greens for a calm environment",        $"pack://application:,,,{ThemesPathPrefix}Forest.xaml",        isDark: true),
                new("highcontrast",  "High Contrast", "Enhanced contrast for accessibility",              $"pack://application:,,,{ThemesPathPrefix}HighContrast.xaml",  isDark: true),
                new("minimalist",    "Minimalist",    "Warm off-white. Ink on linen.",                    $"pack://application:,,,{ThemesPathPrefix}Minimalist.xaml",    isDark: false),
            };
        }

        /// <summary>
        /// Call during OnStartup — loads saved preference and applies it.
        /// </summary>
        public void Initialize ()
        {
            var savedId = _settings.GetSetting("Theme:Current", "dark");
            var theme = AvailableThemes.FirstOrDefault(t => t.Id == savedId)
                       ?? AvailableThemes.First(t => t.Id == "dark");
            SetTheme(theme);
        }

        public void SetTheme ( Theme theme )
        {
            ArgumentNullException.ThrowIfNull(theme);
            if ( ReferenceEquals(theme, _currentTheme) ) return;

            _currentTheme = theme;
            ApplyTheme(theme);
            _settings.SetSetting("Theme:Current", theme.Id);
            ThemeChanged?.Invoke(this, theme);
        }

        public void SetThemeById ( string themeId )
        {
            var theme = AvailableThemes.FirstOrDefault(t => t.Id == themeId);
            if ( theme != null ) SetTheme(theme);
        }

        // ────────────────────────────────────────────────────────────────────
        private void ApplyTheme ( Theme theme )
        {
            var app = Application.Current;
            var merged = app.Resources.MergedDictionaries;

            // Remove existing theme + global styles
            var toRemove = merged
                .Where(d => d.Source?.OriginalString.Contains(ThemesPathPrefix) == true)
                .ToList();
            foreach ( var d in toRemove ) merged.Remove(d);

            // Add new theme dict first
            merged.Add(new ResourceDictionary { Source = new Uri(theme.ResourceUri) });

            // Re-add global control styles (must come AFTER theme so keys resolve)
            merged.Add(new ResourceDictionary { Source = new Uri(GlobalStylesUri) });

            // Propagate dark-mode hint to all open windows
            foreach ( Window w in app.Windows )
                SetWindowDarkMode(w, theme.IsDark);
        }

        // ────────────────────────────────────────────────────────────────────
        // Win32 title-bar dark-mode  (no-op on older Windows gracefully)
        // ────────────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute (
            IntPtr hwnd, int attr, ref int attrValue, int attrSize );

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static void SetWindowDarkMode ( Window window, bool dark )
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if ( hwnd == IntPtr.Zero ) return;
                int value = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
            catch { /* ignore on unsupported Windows versions */ }
        }
    }
}