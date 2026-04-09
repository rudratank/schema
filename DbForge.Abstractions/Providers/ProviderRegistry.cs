using DbForge.Abstractions.Connections;
using DbForge.Abstractions.Providers;
using DbForge.Abstractions.Schema;
using DbForge.Core.Models.Enums;

namespace DbForge.Infrastructure.Providers
{
    public class ProviderRegistry : IProviderRegistry
    {
        private readonly Dictionary<ProviderType, IDbProvider> _providers;
        private readonly Dictionary<ProviderType, ISchemaExtractor> _extractors;

        public ProviderRegistry (
            IEnumerable<IDbProvider> providers,
            IEnumerable<ISchemaExtractor> extractors )
        {
            // DI injects all registered implementations — we index them by type
            _providers = providers.ToDictionary(p => p.ProviderType);
            _extractors = extractors.ToDictionary(e => e.ProviderType);
            // NOTE: ISchemaExtractor needs a ProviderType property — add it to the interface
        }

        public IDbProvider GetProvider ( ProviderType type ) =>
            _providers.TryGetValue(type, out var p) ? p
            : throw new NotSupportedException($"Provider {type} is not registered.");

        public ISchemaExtractor GetExtractor ( ProviderType type ) =>
            _extractors.TryGetValue(type, out var e) ? e
            : throw new NotSupportedException($"Extractor for {type} is not registered.");

        public IEnumerable<ProviderType> GetRegisteredProviders () => _providers.Keys;
    }
}
