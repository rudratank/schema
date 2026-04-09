using DbForge.Core.Connections;
using DbForge.Core.Models.Enums;
using DbForge.WPF.Models;
using DbForge.WPF.UI.Converters;
using DbForge.WPF.UI.Options;
using DbForge.WPF.ViewModels.Base;
using DbForge.WPF.Windows.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DbForge.WPF.ViewModels;

public class ConnectionDialogViewModel : BaseViewModel
{
    private readonly ConnectionService _connectionService;

    public ConnectionDialogViewModel ( ConnectionService connectionService )
    {
        _connectionService = connectionService;

        // Command to test connection without saving
        TestCommand = new RelayCommand(async _ => await TestAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(Host));

        // Command to connect and return result to MainWindow
        ConnectCommand = new RelayCommand(async _ => await ConnectAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(Host));

        // Command to close dialog without doing anything
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    #region #Form Input Fields

    private string _host = string.Empty;
    public string Host
    {
        get => _host;
        set => Set(ref _host, value);
    }

    private int _port = 1433;

    public int Port
    {
        get => _port;
        set => Set(ref _port, value);
    }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    private ProviderType _selectedProvider = ProviderType.SqlServer;
    public ProviderType SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if ( Set(ref _selectedProvider, value) )
            {
                // Auto-adjust default port based on provider
                Port = value == ProviderType.MySql ? 3306 : 1433;
            }
        }
    }
    public IEnumerable<ProviderType> AvailableProviders =>
        new[] { ProviderType.SqlServer, ProviderType.MySql };
    public List<AuthTypeOption> AvailableAuthTypes => new()
    {
        new() { Value = AuthType.Windows, Display = "Windows Authentication" },
        new() { Value = AuthType.SqlPassword, Display = "SQL Server Authentication" }
    };

    /// <summary>
    /// Currently selected authentication type
    /// </summary>
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
                }
            }
        }
    }


    /// <summary>
    /// True only when SQL Authentication is selected
    /// </summary>
    public bool IsSqlAuthentication => SelectedAuthType == AuthType.SqlPassword;

    #endregion

    private bool _isBusy;

    /// <summary>
    /// Indicates background operation (disables UI)
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private string _statusMessage = string.Empty;

    /// <summary>
    /// Status message shown to user (success/error)
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            Set(ref _statusMessage, value);
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    private bool _isStatusSuccess;

    /// <summary>
    /// Indicates success/failure for UI styling
    /// </summary>
    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;
        set => Set(ref _isStatusSuccess, value);
    }

    /// <summary>
    /// Used to toggle visibility of status panel
    /// </summary>
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);


    //  RESULT (Returned after successful connection)
    // ─────────────────────────────────────────────────────────────

    public ServerNode? Result { get; private set; }

    /// <summary>
    /// Event used by View to close dialog
    /// </summary>
    public event Action<bool>? RequestClose;

    #region COMANDS
    public ICommand TestCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand CancelCommand { get; }
    private async Task TestAsync ()
    {
        IsBusy = true;
        StatusMessage = "Testing connection...";
        IsStatusSuccess = false;

        var profile = ConnectionProfileFactory.Create(
         Host,
         Port,
         Username,
         Password,
         SelectedProvider,
         SelectedAuthType);

        var result = await _connectionService.TestConnectionAsync(profile);

        StatusMessage = result.IsSuccess
            ? $"Connected · {result.ServerVersion} · {result.LatencyMs} ms"
            : result.ErrorMessage ?? "Unknown error";

        IsStatusSuccess = result.IsSuccess;
        IsBusy = false;
    }

    private async Task ConnectAsync ()
    {
        IsBusy = true;
        StatusMessage = "Connecting...";
        IsStatusSuccess = false;

        var profile = ConnectionProfileFactory.Create(Host, Port, Username, Password,
            SelectedProvider, SelectedAuthType);

        // Step 1: Validate connection
        var testResult = await _connectionService.TestConnectionAsync(profile);
        if ( !testResult.IsSuccess )
        {
            StatusMessage = testResult.ErrorMessage ?? "Connection failed";
            IsBusy = false;
            return;
        }

        // Step 2: Fetch databases
        IEnumerable<string> databases;
        try
        {
            databases = await _connectionService.GetDatabasesAsync(profile);
        }
        catch
        {
            databases = Array.Empty<string>();
        }

        // Step 3: Build UI node
        Result = new ServerNode
        {
            DisplayName = $"{Host} ({testResult.ServerVersion})",
            ServerVersion = testResult.ServerVersion ?? string.Empty,
            Profile = profile,
            ProviderType = SelectedProvider,
            Databases = new ObservableCollection<string>(databases)
        };

        IsBusy = false;

        // Close dialog and return success
        RequestClose?.Invoke(true);
    }
    #endregion

    #region #Builds connection profile sent to Core layer



    #endregion

}