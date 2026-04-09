using DbForge.Core.Models.Enums;
using System.Data;

namespace DbForge.Abstractions.Connections
{
    // Every DB driver implements this
    // Notice: returns Task — always async. Never block.
    public interface IDbProvider
    {
        ProviderType ProviderType { get; }
        Task<ConnectionTestResult> TestConnectionAsync ( ConnectionProfile profile, CancellationToken ct = default );
        Task<IEnumerable<string>> GetDatabasesAsync ( ConnectionProfile profile, CancellationToken ct = default );
        IDbConnection CreateConnection ( ConnectionProfile profile );
    }
}
