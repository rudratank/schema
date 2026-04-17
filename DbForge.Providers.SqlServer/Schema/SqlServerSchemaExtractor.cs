using DbForge.Abstractions.Schema;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using DbForge.Providers.SqlServer.Extensions;
using System.Data;

namespace DbForge.Providers.SqlServer.Schema;

public class SqlServerSchemaExtractor : ISchemaExtractor
{
    public ProviderType ProviderType => ProviderType.SqlServer;

    public async Task<SchemaModel> ExtractAsync (
        IDbConnection connection,
        IProgress<int>? progress = null,
        CancellationToken ct = default )
    {
        var schema = new SchemaModel
        {
            DatabaseName = connection.Database,
            ProviderType = ProviderType.SqlServer
        };

        progress?.Report(5);
        var columnLookup = await LoadColumnsAsync(connection, ct);

        progress?.Report(15);
        schema.Tables = await LoadTablesAsync(connection, columnLookup, ct);

        progress?.Report(30);
        await AttachIndexesAsync(connection, schema.Tables, ct);

        progress?.Report(42);
        await AttachForeignKeysAsync(connection, schema.Tables, ct);

        progress?.Report(55);
        await AttachProceduresAsync(connection, schema.Procedures, ct);

        // ── NEW object types ──────────────────────────────────────────────────
        progress?.Report(65);
        await AttachViewsAsync(connection, schema.Views, ct);

        progress?.Report(75);
        await AttachFunctionsAsync(connection, schema.Functions, ct);

        progress?.Report(85);
        await AttachTriggersAsync(connection, schema.Triggers, ct);

        progress?.Report(93);
        await AttachSynonymsAsync(connection, schema.Synonyms, ct);

        progress?.Report(100);
        return schema;
    }

    // ───────────────── COLUMNS ─────────────────────────────────────────────────

    private async Task<Dictionary<string, List<ColumnDefinition>>> LoadColumnsAsync (
        IDbConnection conn, CancellationToken ct )
    {
        var lookup = new Dictionary<string, List<ColumnDefinition>>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetColumns;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            var table = reader.GetStringSafe("TABLE_NAME");
            var dataType = reader.GetStringSafe("DATA_TYPE");
            var maxLen = reader.GetNullableInt16Safe("max_length");
            var prec = reader.GetNullableByteSafe("precision");
            var scale = reader.GetNullableByteSafe("scale");

            if ( (dataType == "nvarchar" || dataType == "nchar") && maxLen.HasValue && maxLen > 0 )
                maxLen /= 2;

            if ( !lookup.ContainsKey(table) )
                lookup[table] = new List<ColumnDefinition>();

            lookup[table].Add(new ColumnDefinition
            {
                Name = reader.GetStringSafe("COLUMN_NAME"),
                OrdinalPosition = reader.GetInt32Safe("ORDINAL_POSITION"),
                DataType = dataType,
                FullDataType = BuildFullDataType(dataType, maxLen, prec, scale),
                IsNullable = reader.GetBooleanSafe("is_nullable"),
                IsIdentity = reader.GetBooleanSafe("is_identity"),
                IsPrimaryKey = reader.GetBooleanSafe("is_primary_key"),
                DefaultValue = reader.GetNullableStringSafe("COLUMN_DEFAULT"),
                CharacterMaxLength = maxLen,
                NumericPrecision = prec,
                NumericScale = scale
            });
        }
        return lookup;
    }

    private static string BuildFullDataType ( string dataType, short? maxLen, byte? prec, byte? scale ) =>
        dataType.ToLowerInvariant() switch
        {
            "varchar" or "nvarchar" or "char" or "nchar" or "varbinary" or "binary"
                => maxLen == -1 ? $"{dataType}(max)" : $"{dataType}({maxLen})",
            "decimal" or "numeric"
                => $"{dataType}({prec},{scale})",
            "float" or "real"
                => prec.HasValue ? $"{dataType}({prec})" : dataType,
            _ => dataType
        };

    // ───────────────── TABLES ──────────────────────────────────────────────────

    private async Task<List<TableDefinition>> LoadTablesAsync (
        IDbConnection conn,
        Dictionary<string, List<ColumnDefinition>> columnLookup,
        CancellationToken ct )
    {
        var tables = new List<TableDefinition>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetTables;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            var name = reader.GetStringSafe("TABLE_NAME");
            tables.Add(new TableDefinition
            {
                Name = name,
                SchemaName = reader.GetStringSafe("TABLE_SCHEMA"),
                Columns = columnLookup.TryGetValue(name, out var cols) ? cols : new()
            });
        }
        return tables;
    }

    // ───────────────── INDEXES ─────────────────────────────────────────────────

    private async Task AttachIndexesAsync ( IDbConnection conn, List<TableDefinition> tables, CancellationToken ct )
    {
        var tableMap = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetIndexes;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        var indexMap = new Dictionary<string, Dictionary<string, IndexDefinition>>(StringComparer.OrdinalIgnoreCase);

        while ( await reader.ReadAsync(ct) )
        {
            var tableName = reader.GetStringSafe("TABLE_NAME");
            var indexName = reader.GetStringSafe("INDEX_NAME");

            if ( !indexMap.ContainsKey(tableName) )
                indexMap[tableName] = new(StringComparer.OrdinalIgnoreCase);

            if ( !indexMap[tableName].TryGetValue(indexName, out var idx) )
            {
                idx = new IndexDefinition
                {
                    Name = indexName,
                    IsUnique = reader.GetBooleanSafe("is_unique"),
                    IsPrimaryKey = reader.GetBooleanSafe("is_primary_key"),
                    IsClustered = reader.GetBooleanSafe("is_clustered")
                };
                indexMap[tableName][indexName] = idx;
            }

            idx.Columns.Add(new IndexColumn
            {
                ColumnName = reader.GetStringSafe("COLUMN_NAME"),
                Position = reader.GetInt32Safe("key_ordinal"),
                Descending = reader.GetBooleanSafe("is_descending_key")
            });
        }

        foreach ( var (tableName, indexes) in indexMap )
            if ( tableMap.TryGetValue(tableName, out var table) )
                table.Indexes.AddRange(indexes.Values);
    }

    // ───────────────── FOREIGN KEYS ────────────────────────────────────────────

    private async Task AttachForeignKeysAsync ( IDbConnection conn, List<TableDefinition> tables, CancellationToken ct )
    {
        var tableMap = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetForeignKeys;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        var fkMap = new Dictionary<string, Dictionary<string, ForeignKeyDefinition>>(StringComparer.OrdinalIgnoreCase);

        while ( await reader.ReadAsync(ct) )
        {
            var tableName = reader.GetStringSafe("TABLE_NAME");
            var fkName = reader.GetStringSafe("CONSTRAINT_NAME");

            if ( !fkMap.ContainsKey(tableName) )
                fkMap[tableName] = new(StringComparer.OrdinalIgnoreCase);

            if ( !fkMap[tableName].TryGetValue(fkName, out var fk) )
            {
                fk = new ForeignKeyDefinition
                {
                    Name = fkName,
                    ReferencedTable = reader.GetStringSafe("REFERENCED_TABLE_NAME"),
                    OnDelete = reader.GetNullableStringSafe("delete_referential_action_desc") ?? "NO ACTION",
                    OnUpdate = reader.GetNullableStringSafe("update_referential_action_desc") ?? "NO ACTION"
                };
                fkMap[tableName][fkName] = fk;
            }

            fk.Columns.Add(reader.GetStringSafe("COLUMN_NAME"));
            fk.ReferencedColumns.Add(reader.GetStringSafe("REFERENCED_COLUMN_NAME"));
        }

        foreach ( var (tableName, fks) in fkMap )
            if ( tableMap.TryGetValue(tableName, out var table) )
                table.ForeignKeys.AddRange(fks.Values);
    }

    // ───────────────── PROCEDURES ──────────────────────────────────────────────

    private async Task AttachProceduresAsync ( IDbConnection conn, List<ProcedureDefinition> procedures, CancellationToken ct )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetProcedures;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            procedures.Add(new ProcedureDefinition
            {
                Name = reader.GetStringSafe("PROCEDURE_NAME"),
                SchemaName = reader.GetStringSafe("PROCEDURE_SCHEMA"),
                Definition = reader.GetNullableStringSafe("PROCEDURE_DEFINITION") ?? string.Empty
            });
        }
    }

    // ───────────────── VIEWS ───────────────────────────────────────────────────

    private async Task AttachViewsAsync ( IDbConnection conn, List<ViewDefinition> views, CancellationToken ct )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetViews;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            views.Add(new ViewDefinition
            {
                Name = reader.GetStringSafe("VIEW_NAME"),
                SchemaName = reader.GetStringSafe("VIEW_SCHEMA"),
                Definition = reader.GetNullableStringSafe("VIEW_DEFINITION") ?? string.Empty,
                IsSchemaBound = reader.GetBooleanSafe("is_schema_bound"),
                IsIndexed = reader.GetBooleanSafe("is_indexed")
            });
        }
    }

    // ───────────────── FUNCTIONS ───────────────────────────────────────────────

    private async Task AttachFunctionsAsync ( IDbConnection conn, List<FunctionDefinition> functions, CancellationToken ct )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetFunctions;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            var typeCode = reader.GetStringSafe("FUNCTION_TYPE").Trim().ToUpperInvariant();
            var fnType = typeCode switch
            {
                "IF" => SqlFunctionType.InlineTableValued,
                "TF" => SqlFunctionType.MultiStatementTableValued,
                _ => SqlFunctionType.Scalar   // FN
            };

            var def = reader.GetNullableStringSafe("FUNCTION_DEFINITION") ?? string.Empty;

            functions.Add(new FunctionDefinition
            {
                Name = reader.GetStringSafe("FUNCTION_NAME"),
                SchemaName = reader.GetStringSafe("FUNCTION_SCHEMA"),
                Definition = def,
                FunctionType = fnType,
                ReturnType = FunctionBodyParser.ExtractReturnType(def)
            });
        }
    }

    // ───────────────── TRIGGERS ────────────────────────────────────────────────

    private async Task AttachTriggersAsync ( IDbConnection conn, List<TriggerDefinition> triggers, CancellationToken ct )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetTriggers;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            var events = TriggerEvents.None;
            if ( reader.GetBooleanSafe("fires_on_insert") ) events |= TriggerEvents.Insert;
            if ( reader.GetBooleanSafe("fires_on_update") ) events |= TriggerEvents.Update;
            if ( reader.GetBooleanSafe("fires_on_delete") ) events |= TriggerEvents.Delete;

            triggers.Add(new TriggerDefinition
            {
                Name = reader.GetStringSafe("TRIGGER_NAME"),
                SchemaName = reader.GetStringSafe("TRIGGER_SCHEMA"),
                Definition = reader.GetNullableStringSafe("TRIGGER_DEFINITION") ?? string.Empty,
                ParentTable = reader.GetStringSafe("PARENT_TABLE"),
                Events = events,
                Timing = reader.GetBooleanSafe("is_instead_of_trigger")
                                  ? TriggerTiming.InsteadOf
                                  : TriggerTiming.After,
                IsEnabled = !reader.GetBooleanSafe("is_disabled")
            });
        }
    }

    // ───────────────── SYNONYMS ────────────────────────────────────────────────

    private async Task AttachSynonymsAsync ( IDbConnection conn, List<SynonymDefinition> synonyms, CancellationToken ct )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlServerSchemaQueries.GetSynonyms;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while ( await reader.ReadAsync(ct) )
        {
            synonyms.Add(new SynonymDefinition
            {
                Name = reader.GetStringSafe("SYNONYM_NAME"),
                SchemaName = reader.GetStringSafe("SYNONYM_SCHEMA"),
                BaseObjectName = reader.GetStringSafe("BASE_OBJECT_NAME")
            });
        }
    }
}

// ── Helper to parse function RETURNS clause at extraction time ────────────────
internal static class FunctionBodyParser
{
    private static readonly System.Text.RegularExpressions.Regex ReturnTypeRegex =
        new(@"\bRETURNS\b\s+(TABLE|[\w\s\(\),]+?)\s+\b(?:AS|WITH)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);

    public static string ExtractReturnType ( string sql )
    {
        if ( string.IsNullOrWhiteSpace(sql) ) return string.Empty;
        var m = ReturnTypeRegex.Match(sql);
        return m.Success
            ? System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value.Trim(), @"\s+", " ")
            : string.Empty;
    }
}