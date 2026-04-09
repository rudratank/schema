using DbForge.WPF.Models;
using DbForge.WPF.ViewModels.Base;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels;

public class MainViewModel : BaseViewModel
{
    // The left-panel tree binds to this
    public ObservableCollection<ServerNode> Servers { get; } = new();

    private string _statusText = "Ready  ·  Not connected";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public MainViewModel ()
    {
        Servers.CollectionChanged += ( _, _ ) => OnPropertyChanged(nameof(HasServers));
    }

    public bool HasServers => Servers.Count > 0;

    public void AddServer ( ServerNode server )
    {
        Servers.Add(server);
        StatusText = $"Connected  ·  {server.DisplayName}";
    }
}