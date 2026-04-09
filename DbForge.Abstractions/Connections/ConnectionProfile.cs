using DbForge.Core.Models.Enums;
namespace DbForge.Abstractions.Connections
{
    public class ConnectionProfile
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string? Username { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
        public AuthType AuthType { get; set; }
        public ProviderType ProviderType { get; set; }
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string> AdditionalParameters { get; set; } = new();
    }
}
