using DbForge.Core.Connections;
using System.Windows.Input;

namespace DbForge.UI.ViewModels
{
    public class ConnectionsViewModel : BaseViewModel
    {
        private readonly ConnectionService _connectionService;

        // Bound to form fields
        private string _host = string.Empty;
        public string Host { get => _host; set => Set(ref _host, value); }

        // Bound to status label
        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

        public ICommand TestConnectionCommand { get; }

        public ConnectionsViewModel ( ConnectionService connectionService )
        {
            _connectionService = connectionService;
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        }

        private async Task TestConnectionAsync ()
        {
            IsBusy = true;
            StatusMessage = "Testing...";

            var profile = BuildProfile();   // read Host, Port, etc.
            var result = await _connectionService.TestConnectionAsync(profile);

            StatusMessage = result.IsSuccess
                ? $"✓ Connected — {result.ServerVersion} ({result.LatencyMs}ms)"
                : $"✗ {result.ErrorMessage}";

            IsBusy = false;
        }
    }
}
