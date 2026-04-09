using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DbForge.WPF.ViewModels.Base
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged ( [CallerMemberName] string? name = null )
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Returns true if value changed — use this in every property setter
        protected bool Set<T> ( ref T field, T value, [CallerMemberName] string? name = null )
        {
            if ( EqualityComparer<T>.Default.Equals(field, value) ) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
