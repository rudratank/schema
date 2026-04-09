using DbForge.Abstractions.Connections;
using DbForge.Core.Models.Enums;

namespace DbForge.WPF.UI.Converters
{
    public static class ConnectionProfileFactory
    {
        public static ConnectionProfile Create (
            string host, int port, string username, string password,
            ProviderType provider, AuthType auth, string? db = null )
        {
            return new ConnectionProfile
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                ProviderType = provider,
                AuthType = auth,
                DatabaseName = db ?? string.Empty
            };
        }
    }
}
