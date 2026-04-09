using DbForge.Abstractions.Connections;
using DbForge.Core.Connections;
using DbForge.Core.Models.Enums;
using DbForge.WPF.UI.Converters;
using DbForge.WPF.UI.Options;
using DbForge.WPF.ViewModels.Base;
using DbForge.WPF.Windows.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DbForge.WPF.ViewModels.Compare;

public class ConnectionSideViewModel : BaseViewModel
{
    private readonly ConnectionService _connectionService;

    public ConnectionSideViewModel ( ConnectionService connectionService )
    {
        _connectionService = connectionService;

        ConnectCommand = new RelayCommand(
            async _ => await TestAndLoadDatabasesAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(Host));
    }

    // ── Form fields ──────────────────────────────────────────────────────────

    private string _host = string.Empty;
    public string Host
    {
        get => _host;
        set { if ( Set(ref _host, value) ) CommandManager.InvalidateRequerySuggested(); }
    }

    private int _port = 1433;
    public int Port { get => _port; set => Set(ref _port, value); }

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    private ProviderType _selectedProvider = ProviderType.SqlServer;
    public ProviderType SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if ( Set(ref _selectedProvider, value) )
                Port = value == ProviderType.MySql ? 3306 : 1433;
        }
    }

    public IEnumerable<ProviderType> AvailableProviders =>
        new[] { ProviderType.SqlServer, ProviderType.MySql };

    public List<AuthTypeOption> AvailableAuthTypes => new()
    {
        new() { Value = AuthType.Windows,     Display = "Windows Authentication"      },
        new() { Value = AuthType.SqlPassword, Display = "SQL Server Authentication"   }
    };

    private AuthType _selectedAuthType = AuthType.Windows;
    public AuthType SelectedAuthType
    {
        get => _selectedAuthType;
        set
        {
            if ( Set(ref _selectedAuthType, value) )
            {
                OnPropertyChanged(nameof(IsSqlAuthentication));
                if ( value == AuthType.Windows )
                {
                    Username = string.Empty;
                    Password = string.Empty;
                    OnPropertyChanged(nameof(Username));
                    OnPropertyChanged(nameof(Password));
                }
            }
        }
    }

    public bool IsSqlAuthentication => SelectedAuthType == AuthType.SqlPassword;

    // ── State ────────────────────────────────────────────────────────────────

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { Set(ref _isBusy, value); CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set => Set(ref _isConnected, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { Set(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatus)); }
    }

    private bool _isStatusSuccess;
    public bool IsStatusSuccess { get => _isStatusSuccess; set => Set(ref _isStatusSuccess, value); }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    // ── Database list ────────────────────────────────────────────────────────

    public ObservableCollection<string> Databases { get; } = new();

    private string? _selectedDatabase;
    public string? SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            Set(ref _selectedDatabase, value);
            OnPropertyChanged(nameof(HasDatabaseSelected));
            DatabaseSelected?.Invoke();
        }
    }

    public bool HasDatabaseSelected => !string.IsNullOrEmpty(SelectedDatabase);

    /// <summary>Raised when the user picks a database — CompareSetupViewModel re-checks CanExecute.</summary>
    public event Action? DatabaseSelected;

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand ConnectCommand { get; }

    // ── Logic ────────────────────────────────────────────────────────────────

    private async Task TestAndLoadDatabasesAsync ()
    {
        IsBusy = true;
        IsConnected = false;
        IsStatusSuccess = false;
        StatusMessage = "Connecting…";
        Databases.Clear();
        SelectedDatabase = null;

        var profile = ConnectionProfileFactory.Create(Host, Port, Username, Password,
     SelectedProvider, SelectedAuthType, SelectedDatabase);
        var result = await _connectionService.TestConnectionAsync(profile);

        if ( !result.IsSuccess )
        {
            StatusMessage = result.ErrorMessage ?? "Connection failed";
            IsBusy = false;
            return;
        }

        try
        {
            var dbs = await _connectionService.GetDatabasesAsync(profile);

            // Add in batches so the UI stays responsive even for 1 000+ databases
            var sorted = dbs.OrderBy(d => d).ToList();
            const int batchSize = 200;
            for ( int i = 0; i < sorted.Count; i += batchSize )
            {
                foreach ( var db in sorted.Skip(i).Take(batchSize) )
                    Databases.Add(db);

                // Yield to the dispatcher so the list renders incrementally
                await Task.Yield();
            }

            IsConnected = true;
            IsStatusSuccess = true;
            StatusMessage = $"Connected  ·  {result.ServerVersion}  ·  {result.LatencyMs} ms  ·  {Databases.Count} databases";
        }
        catch ( Exception ex )
        {
            StatusMessage = $"Connected but could not load databases: {ex.Message}";
        }

        IsBusy = false;
    }

    public void SetFrom ( ConnectionProfile profile )
    {
        Host = profile.Host;
        Port = profile.Port;
        Username = profile.Username ?? string.Empty;
        Password = profile.Password ?? string.Empty;
        SelectedProvider = profile.ProviderType;
        SelectedDatabase = profile.DatabaseName;

        OnPropertyChanged(nameof(Host));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedDatabase));
    }
}