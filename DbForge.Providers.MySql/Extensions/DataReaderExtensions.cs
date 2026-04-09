using System.Data;
using System.Data.Common;

namespace DbForge.Providers.MySql.Extensions;

internal static class DataReaderExtensions
{
    public static async Task<bool> ReadAsync ( this IDataReader reader, CancellationToken ct = default )
    {
        ct.ThrowIfCancellationRequested();
        if ( reader is DbDataReader dbReader )
            return await dbReader.ReadAsync(ct);
        return await Task.Run(reader.Read, ct);
    }

    public static bool IsDBNull ( this IDataReader reader, string col )
        => reader.IsDBNull(reader.GetOrdinal(col));

    public static string GetString ( this IDataReader reader, string col )
        => reader.GetString(reader.GetOrdinal(col));

    public static int GetInt32 ( this IDataReader reader, string col )
        => reader.GetInt32(reader.GetOrdinal(col));
}