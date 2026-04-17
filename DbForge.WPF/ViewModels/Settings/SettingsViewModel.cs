using DbForge.WPF.Models;
using DbForge.WPF.Services;
using DbForge.WPF.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace DbForge.WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for a single theme card in the settings picker.
    /// Exposes a PreviewBrush (the card's top color swatch) and a
    /// two-way IsSelected that the RadioButton binds to.
    /// </summary>
    public class ThemeItemViewModel : BaseViewModel
    {
        private bool _isSelected;

        public Theme Theme { get; }

        /// <summary>Display name — bound directly from Theme.Name.</summary>
        public string Name => Theme.Name;
        /// <summary>Short description.</summary>
        public string Description => Theme.Description;

        /// <summary>
        /// Solid-color brush shown in the card preview strip.
        /// Derived from the theme id so no ResourceDictionary lookup is needed here.
        /// </summary>
        public Brush PreviewBrush { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public ThemeItemViewModel ( Theme theme )
        {
            Theme = theme;
            PreviewBrush = BuildPreview(theme.Id);
        }

        /// <summary>
        /// Returns a representative solid brush for each theme without
        /// needing to load the XAML resource dictionary at this point.
        /// </summary>
        private static Brush BuildPreview ( string themeId ) => themeId switch
        {
            "dark" => new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            "light" => new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA)),
            "ocean" => new SolidColorBrush(Color.FromRgb(0x06, 0x0D, 0x1F)),
            "forest" => new SolidColorBrush(Color.FromRgb(0x0A, 0x0F, 0x0A)),
            "highcontrast" => new SolidColorBrush(Colors.Black),
            "minimalist" => new SolidColorBrush(Color.FromRgb(0xF5, 0xF2, 0xEE)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    /// <summary>
    /// Settings page ViewModel.
    /// The theme cards use RadioButton-style IsSelected binding;
    /// ApplyThemeCommand commits the choice to ThemeService + persists it.
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly ThemeService _themeService;
        private ThemeItemViewModel? _selectedThemeItem;
        private string _editorFontSize = "12";
        private string _editorFontFamily = "Consolas";

        public SettingsViewModel ( ThemeService themeService )
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // Build card VMs for every registered theme
            AvailableThemes = new ObservableCollection<ThemeItemViewModel>(
                _themeService.AvailableThemes
                             .Select(t => new ThemeItemViewModel(t)));

            // Pre-select the active theme
            var current = AvailableThemes
                .FirstOrDefault(t => t.Theme.Id == _themeService.CurrentTheme?.Id)
                ?? AvailableThemes.FirstOrDefault();

            if ( current != null )
            {
                current.IsSelected = true;
                _selectedThemeItem = current;
            }

            ApplyThemeCommand = new RelayCommand(ApplyTheme, CanApplyTheme);
        }

        // ── Bindable collections ─────────────────────────────────────────────
        public ObservableCollection<ThemeItemViewModel> AvailableThemes { get; }

        // ── Selected theme (card click sets IsSelected on item + this) ───────
        public ThemeItemViewModel? SelectedThemeItem
        {
            get => _selectedThemeItem;
            set
            {
                if ( _selectedThemeItem == value ) return;
                if ( _selectedThemeItem != null )
                    _selectedThemeItem.IsSelected = false;
                Set(ref _selectedThemeItem, value);
                if ( value != null )
                    value.IsSelected = true;
                OnPropertyChanged(nameof(SelectedTheme));
            }
        }

        /// <summary>Convenience — the Theme model of the selected card.</summary>
        public Theme? SelectedTheme => _selectedThemeItem?.Theme;

        // ── Editor preferences ────────────────────────────────────────────────
        public string EditorFontSize
        {
            get => _editorFontSize;
            set => Set(ref _editorFontSize, value);
        }

        public string EditorFontFamily
        {
            get => _editorFontFamily;
            set => Set(ref _editorFontFamily, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand ApplyThemeCommand { get; }

        private bool CanApplyTheme () => _selectedThemeItem != null;

        private void ApplyTheme ()
        {
            // Walk all cards and honour the RadioButton IsSelected state
            var chosen = AvailableThemes.FirstOrDefault(t => t.IsSelected);
            if ( chosen == null ) return;

            _selectedThemeItem = chosen;
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(SelectedThemeItem));

            _themeService.SetTheme(chosen.Theme);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Minimal RelayCommand (parameter-less overload matching original code)
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand ( Action execute, Func<bool>? canExecute = null )
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => System.Windows.Input.CommandManager.RequerySuggested += value;
            remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute ( object? _ ) => _canExecute?.Invoke() ?? true;
        public void Execute ( object? _ ) => _execute();
    }
}
