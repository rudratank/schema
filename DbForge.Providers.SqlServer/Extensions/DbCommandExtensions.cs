using System.Data;
using System.Data.Common;

namespace DbForge.Providers.SqlServer.Extensions
{
    internal static class DbCommandExtensions
    {
        public static void AddParameter ( this IDbCommand cmd, string name, object? value )
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        public static async Task<IDataReader> ExecuteReaderAsync (
            this IDbCommand cmd, CancellationToken ct = default )
        {
            ct.ThrowIfCancellationRequested();

            if ( cmd is DbCommand dbCmd )
                return await dbCmd.ExecuteReaderAsync(ct);

            return await Task.Run(cmd.ExecuteReader, ct);
        }
    }
}