using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using System.Data;

namespace DbForge.Abstractions.Schema;

public interface ISchemaExtractor
{
    ProviderType ProviderType { get; }   // needed by ProviderRegistry.ToDictionary()

    Task<SchemaModel> ExtractAsync (
        IDbConnection connection,
        IProgress<int>? progress = null,
        CancellationToken ct = default );
}