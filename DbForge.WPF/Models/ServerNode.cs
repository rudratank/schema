using DbForge.Abstractions.Connections;
using DbForge.Core.Models.Enums;
using System.Collections.ObjectModel;

namespace DbForge.WPF.Models
{
    public class ServerNode
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ServerVersion { get; set; } = string.Empty;
        public ConnectionProfile Profile { get; set; } = new();
        public ProviderType ProviderType { get; set; }
        public ObservableCollection<string> Databases { get; set; } = new();
        public bool IsExpanded { get; set; } = true;
    }
}