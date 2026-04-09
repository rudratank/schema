using DbForge.Abstractions.Connections;
using DbForge.Core.Models.Enums;
using MySqlConnector;
using System.Data;
using System.Diagnostics;

namespace DbForge.Providers.MySql
{
    public class MySqlDbProvider : IDbProvider
    {
        public ProviderType ProviderType => ProviderType.MySql;

        public async Task<ConnectionTestResult> TestConnectionAsync (
            ConnectionProfile profile, CancellationToken ct = default )
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var conn = new MySqlConnection(BuildConnectionString(profile));
                await conn.OpenAsync(ct);
                var version = conn.ServerVersion;
                sw.Stop();
                return ConnectionTestResult.Success(version, sw.ElapsedMilliseconds);
            }
            catch ( Exception ex )
            {
                return ConnectionTestResult.Failure(ex.Message);
            }
        }

        public async Task<IEnumerable<string>> GetDatabasesAsync (
            ConnectionProfile profile, CancellationToken ct = default )
        {
            using var conn = new MySqlConnection(BuildConnectionString(profile));
            await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SHOW DATABASES";
            var databases = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while ( await reader.ReadAsync(ct) )
                databases.Add(reader.GetString(0));
            return databases;
        }

        public IDbConnection CreateConnection ( ConnectionProfile profile ) =>
            new MySqlConnection(BuildConnectionString(profile));

        private static string BuildConnectionString ( ConnectionProfile p ) =>
            $"Server={p.Host};Port={p.Port};Database={p.DatabaseName};" +
            $"User={p.Username};Password={p.Password};" +
            $"Connection Timeout={p.ConnectionTimeoutSeconds};";
    }
}
