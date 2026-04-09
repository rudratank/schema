using DbForge.Abstractions.Compare;
using DbForge.Abstractions.Connections;
using DbForge.Abstractions.Extensions;
using DbForge.Abstractions.Providers;
using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Schema
{
    public class SchemaCompareEngine
    {
        private readonly IProviderRegistry _providerRegistry;
        private readonly ISchemaComparer _comparer;

        public SchemaCompareEngine ( IProviderRegistry providerRegistry, ISchemaComparer comparer )
        {
            _providerRegistry = providerRegistry;
            _comparer = comparer;
        }

        public async Task<CompareExecutionResult> CompareAsync (
            ConnectionProfile sourceProfile,
            ConnectionProfile targetProfile,
            IProgress<CompareProgressEvent>? progress = null,
            CancellationToken ct = default )
        {
            progress?.Report(new CompareProgressEvent("Extracting source schema...", 10));
            var sourceSchema = await ExtractSchemaAsync(sourceProfile,
                p => progress?.Report(new CompareProgressEvent("Extracting source schema...", 10 + p / 5)), ct);

            ct.ThrowIfCancellationRequested();

            progress?.Report(new CompareProgressEvent("Extracting target schema...", 30));
            var targetSchema = await ExtractSchemaAsync(targetProfile,
                p => progress?.Report(new CompareProgressEvent("Extracting target schema...", 30 + p / 5)), ct);

            ct.ThrowIfCancellationRequested();

            progress?.Report(new CompareProgressEvent("Comparing schemas...", 60));
            var result = _comparer.Compare(sourceSchema, targetSchema);
            result.SourceDatabase = sourceProfile.DatabaseName;
            result.TargetDatabase = targetProfile.DatabaseName;

            progress?.Report(new CompareProgressEvent("Done", 100));

            return new CompareExecutionResult
            {
                Result = result,
                SourceSchema = sourceSchema,
                TargetSchema = targetSchema,
            };
        }

        private async Task<SchemaModel> ExtractSchemaAsync (
            ConnectionProfile profile,
            Action<int>? onProgress,
            CancellationToken ct )
        {
            var provider = _providerRegistry.GetProvider(profile.ProviderType);
            var extractor = _providerRegistry.GetExtractor(profile.ProviderType);
            using var connection = provider.CreateConnection(profile);
            await connection.OpenAsync(ct);
            var progress = onProgress != null ? new Progress<int>(onProgress) : null;
            return await extractor.ExtractAsync(connection, progress, ct);
        }
    }

    // ── Result types ──────────────────────────────────────────────────────────

    /// <summary>
    /// Everything produced by one compare run.
    /// The mapper converts this into a CompareResultViewModel.
    /// </summary>
    public sealed class CompareExecutionResult
    {
        public CompareResult Result { get; init; } = new();
        public SchemaModel SourceSchema { get; init; } = new();
        public SchemaModel TargetSchema { get; init; } = new();
    }

    public record CompareProgressEvent ( string Message, int PercentComplete );
}