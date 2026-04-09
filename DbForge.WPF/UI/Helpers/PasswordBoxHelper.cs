using System.Windows;
using System.Windows.Controls;

namespace DbForge.WPF.UI.Helpers
{
    public static class PasswordBoxHelper
    {
        // ── BoundPassword (two-way bindable string) ──────────────────────────────
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword", typeof(string), typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static string GetBoundPassword ( DependencyObject d ) =>
            ( string ) d.GetValue(BoundPasswordProperty);

        public static void SetBoundPassword ( DependencyObject d, string v ) =>
            d.SetValue(BoundPasswordProperty, v);

        private static bool _updating;

        private static void OnBoundPasswordChanged (
            DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            if ( d is not PasswordBox box || _updating ) return;
            box.PasswordChanged -= OnPasswordChanged;
            box.Password = ( string ) (e.NewValue ?? string.Empty);
            box.PasswordChanged += OnPasswordChanged;
        }

        // ── BindPassword (set to True to activate) ───────────────────────────────
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword", typeof(bool), typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        public static bool GetBindPassword ( DependencyObject d ) =>
            ( bool ) d.GetValue(BindPasswordProperty);

        public static void SetBindPassword ( DependencyObject d, bool v ) =>
            d.SetValue(BindPasswordProperty, v);

        private static void OnBindPasswordChanged (
            DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            if ( d is not PasswordBox box ) return;
            if ( ( bool ) e.NewValue )
                box.PasswordChanged += OnPasswordChanged;
            else
                box.PasswordChanged -= OnPasswordChanged;
        }

        private static void OnPasswordChanged ( object sender, RoutedEventArgs e )
        {
            if ( sender is not PasswordBox box ) return;
            _updating = true;
            SetBoundPassword(box, box.Password);
            _updating = false;
        }
    }
}
