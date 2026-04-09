using System.Data;
using System.Data.Common;

namespace DbForge.Abstractions.Extensions
{
    public static class DbConnectionExtensions
    {
        public static Task OpenAsync ( this IDbConnection connection, CancellationToken ct = default )
        {
            if ( connection is DbConnection dbConn )
                return dbConn.OpenAsync(ct);
            connection.Open();
            return Task.CompletedTask;
        }
    }
}
