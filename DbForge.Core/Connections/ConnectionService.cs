using DbForge.Abstractions.Connections;
using DbForge.Abstractions.Providers;

namespace DbForge.Core.Connections;

public class ConnectionService
{
    private readonly IProviderRegistry _providerRegistry;
    private readonly IConnectionRepository _repository;

    public ConnectionService ( IProviderRegistry providerRegistry, IConnectionRepository repository )
    {
        _providerRegistry = providerRegistry;
        _repository = repository;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync (
        ConnectionProfile profile, CancellationToken ct = default )
    {
        var provider = _providerRegistry.GetProvider(profile.ProviderType);
        return await provider.TestConnectionAsync(profile, ct);
    }

    public async Task<SavedConnection> SaveConnectionAsync ( string name, ConnectionProfile profile )
    {
        var connection = new SavedConnection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Profile = profile,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.SaveAsync(connection);
        return connection;
    }

    // Add inside ConnectionService class
    public Task<IEnumerable<string>> GetDatabasesAsync ( ConnectionProfile profile, CancellationToken ct = default )
    {
        var provider = _providerRegistry.GetProvider(profile.ProviderType);
        return provider.GetDatabasesAsync(profile, ct);
    }

    public Task<IEnumerable<SavedConnection>> GetAllAsync () => _repository.GetAllAsync();

    public Task<SavedConnection?> GetByIdAsync ( Guid id ) => _repository.GetByIdAsync(id);  // ADD

    public Task DeleteAsync ( Guid id ) => _repository.DeleteAsync(id);
}