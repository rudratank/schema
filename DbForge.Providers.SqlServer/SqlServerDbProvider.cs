using DbForge.Abstractions.Connections;
using DbForge.Core.Models.Enums;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace DbForge.Providers.SqlServer
{
    public class SqlServerDbProvider : IDbProvider
    {
        public ProviderType ProviderType => ProviderType.SqlServer;

        public async Task<ConnectionTestResult> TestConnectionAsync (
            ConnectionProfile profile, CancellationToken ct = default )
        {
            var sw = Stopwatch.StartNew();

            try
            {
                using var conn = new SqlConnection(BuildConnectionString(profile));
                await conn.OpenAsync(ct);

                sw.Stop();
                return ConnectionTestResult.Success(conn.ServerVersion, sw.ElapsedMilliseconds);
            }
            catch ( Exception ex )
            {
                return ConnectionTestResult.Failure(ex.Message);
            }
        }

        public async Task<IEnumerable<string>> GetDatabasesAsync (
            ConnectionProfile profile, CancellationToken ct = default )
        {
            using var conn = new SqlConnection(BuildConnectionString(profile));
            await conn.OpenAsync(ct);

            var list = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sys.databases";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while ( await reader.ReadAsync(ct) )
                list.Add(reader.GetString(0));

            return list;
        }

        public IDbConnection CreateConnection ( ConnectionProfile profile )
            => new SqlConnection(BuildConnectionString(profile));

        private static string BuildConnectionString ( ConnectionProfile p )
        {
            if ( p.AuthType == AuthType.Windows )
            {
                return $"Server={p.Host};Database={p.DatabaseName};" +
                       $"Integrated Security=True;" +
                       $"TrustServerCertificate=True;";
            }

            return $"Server={p.Host};Database={p.DatabaseName};" +
                   $"User Id={p.Username};Password={p.Password};" +
                   $"TrustServerCertificate=True;";
        }
    }
}