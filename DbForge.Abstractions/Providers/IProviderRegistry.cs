using DbForge.Abstractions.Connections;
using DbForge.Abstractions.Schema;
using DbForge.Core.Models.Enums;

namespace DbForge.Abstractions.Providers;

public interface IProviderRegistry
{
    IDbProvider GetProvider ( ProviderType type );
    ISchemaExtractor GetExtractor ( ProviderType type );
    IEnumerable<ProviderType> GetRegisteredProviders ();
}