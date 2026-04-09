using System.Data;
using System.Data.Common;

namespace DbForge.Providers.SqlServer.Extensions;

internal static class DataReaderExtensions
{
    public static async Task<bool> ReadAsync ( this IDataReader reader, CancellationToken ct = default )
    {
        ct.ThrowIfCancellationRequested();

        if ( reader is DbDataReader dbReader )
            return await dbReader.ReadAsync(ct);

        return await Task.Run(reader.Read, ct);
    }

    // ── Core safe accessor ───────────────────────────────────────────────────

    private static object GetValue ( this IDataReader reader, string col )
        => reader[reader.GetOrdinal(col)];

    // ── Null helpers ─────────────────────────────────────────────────────────

    public static bool IsDBNull ( this IDataReader reader, string col )
        => reader.IsDBNull(reader.GetOrdinal(col));

    // ── SAFE STRING ──────────────────────────────────────────────────────────

    public static string GetStringSafe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? string.Empty : val.ToString()!;
    }

    public static string? GetNullableStringSafe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? null : val.ToString();
    }

    // ── SAFE INT ─────────────────────────────────────────────────────────────

    public static int GetInt32Safe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? 0 : Convert.ToInt32(val);
    }

    public static short GetInt16Safe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? ( short ) 0 : Convert.ToInt16(val);
    }

    public static short? GetNullableInt16Safe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? null : Convert.ToInt16(val);
    }

    public static byte? GetNullableByteSafe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);
        return val == DBNull.Value ? null : Convert.ToByte(val);
    }

    // ── SAFE BOOLEAN (CRITICAL FIX) ──────────────────────────────────────────

    public static bool GetBooleanSafe ( this IDataReader reader, string col )
    {
        var val = reader.GetValue(col);

        if ( val == DBNull.Value ) return false;

        return val switch
        {
            bool b => b,
            byte bt => bt == 1,
            short s => s == 1,
            int i => i == 1,
            long l => l == 1,
            string str => str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToBoolean(val)
        };
    }
}