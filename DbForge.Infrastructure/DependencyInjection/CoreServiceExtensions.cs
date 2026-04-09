using DbForge.Abstractions.Compare;
using DbForge.Abstractions.Connections;
using DbForge.Abstractions.Providers;
using DbForge.Abstractions.Schema;
using DbForge.Core.Compare;
using DbForge.Core.Connections;
using DbForge.Core.Schema;
using DbForge.Infrastructure.Persistence;
using DbForge.Infrastructure.Providers;
using DbForge.Providers.MySql;
using DbForge.Providers.MySql.Schema;
using DbForge.Providers.SqlServer;
using DbForge.Providers.SqlServer.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DbForge.Infrastructure.DependencyInjection;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddDbForgeCore ( this IServiceCollection services )
    {
        services.AddScoped<ConnectionService>();

        // SchemaCompareEngine registered ONCE here as Scoped.
        // (Removed duplicate Singleton registration from AddDbForgeProviders.)
        services.AddScoped<SchemaCompareEngine>();

        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite("Data Source=dbforge.db"));

        services.AddScoped<IConnectionRepository, SqliteConnectionRepository>();

        return services;
    }

    public static IServiceCollection AddDbForgeProviders ( this IServiceCollection services )
    {
        // Providers
        services.AddSingleton<IDbProvider, MySqlDbProvider>();
        services.AddSingleton<IDbProvider, SqlServerDbProvider>();

        // Schema extractors
        services.AddSingleton<ISchemaExtractor, MySqlSchemaExtractor>();
        services.AddSingleton<ISchemaExtractor, SqlServerSchemaExtractor>();

        // Schema comparer (stateless — safe as Singleton)
        services.AddSingleton<ISchemaComparer, SchemaComparer>();

        return services;
    }
}