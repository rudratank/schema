using DbForge.Abstractions.Schema;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;
using DbForge.Providers.MySql.Extensions;
using System.Data;

namespace DbForge.Providers.MySql.Schema
{
    public class MySqlSchemaExtractor : ISchemaExtractor
    {
        public ProviderType ProviderType => ProviderType.MySql;

        public async Task<SchemaModel> ExtractAsync (
            IDbConnection connection,
            IProgress<int>? progress = null,
            CancellationToken ct = default )
        {
            var dbName = connection.Database;

            var schema = new SchemaModel
            {
                DatabaseName = dbName,
                ProviderType = ProviderType.MySql
            };

            // Step 1: Load columns
            progress?.Report(10);
            var columnLookup = await LoadColumnsAsync(connection, dbName, ct);

            // Step 2: Load tables
            progress?.Report(40);
            schema.Tables = await LoadTablesAsync(connection, dbName, columnLookup, ct);

            // Step 3: Indexes (later)
            progress?.Report(70);
            await AttachIndexesAsync(connection, dbName, schema.Tables, ct);

            // Step 4: Foreign Keys (later)
            progress?.Report(90);
            await AttachForeignKeysAsync(connection, dbName, schema.Tables, ct);

            progress?.Report(100);
            return schema;
        }

        private async Task<Dictionary<string, List<ColumnDefinition>>> LoadColumnsAsync (
            IDbConnection conn, string dbName, CancellationToken ct )
        {
            var lookup = new Dictionary<string, List<ColumnDefinition>>(StringComparer.OrdinalIgnoreCase);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = MySqlSchemaQueries.GetColumns;
            cmd.AddParameter("@DatabaseName", dbName);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while ( await reader.ReadAsync(ct) )
            {
                var tableName = reader.GetString("TABLE_NAME");

                if ( !lookup.ContainsKey(tableName) )
                    lookup[tableName] = new List<ColumnDefinition>();

                lookup[tableName].Add(new ColumnDefinition
                {
                    Name = reader.GetString("COLUMN_NAME"),
                    OrdinalPosition = reader.GetInt32("ORDINAL_POSITION"),
                    DataType = reader.GetString("DATA_TYPE"),
                    FullDataType = reader.GetString("COLUMN_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT"),
                    IsIdentity = reader.GetString("EXTRA").Contains("auto_increment"),
                    IsPrimaryKey = reader.GetString("COLUMN_KEY") == "PRI",
                    CharacterMaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH")
                        ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                    Comment = reader.IsDBNull("COLUMN_COMMENT") ? null : reader.GetString("COLUMN_COMMENT"),
                });
            }

            return lookup;
        }

        private async Task<List<TableDefinition>> LoadTablesAsync (
            IDbConnection conn,
            string dbName,
            Dictionary<string, List<ColumnDefinition>> columnLookup,
            CancellationToken ct )
        {
            var tables = new List<TableDefinition>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = MySqlSchemaQueries.GetTables;
            cmd.AddParameter("@DatabaseName", dbName);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while ( await reader.ReadAsync(ct) )
            {
                var tableName = reader.GetString("TABLE_NAME");

                tables.Add(new TableDefinition
                {
                    Name = tableName,
                    SchemaName = reader.GetString("TABLE_SCHEMA"),
                    Comment = reader.IsDBNull("TABLE_COMMENT") ? null : reader.GetString("TABLE_COMMENT"),
                    Engine = reader.IsDBNull("ENGINE") ? string.Empty : reader.GetString("ENGINE"),
                    Columns = columnLookup.TryGetValue(tableName, out var cols) ? cols : new()
                });
            }

            return tables;
        }

        private async Task AttachIndexesAsync (
            IDbConnection conn,
            string dbName,
            List<TableDefinition> tables,
            CancellationToken ct )
        {
            await Task.CompletedTask; // TODO later
        }

        private async Task AttachForeignKeysAsync (
            IDbConnection conn,
            string dbName,
            List<TableDefinition> tables,
            CancellationToken ct )
        {
            await Task.CompletedTask; // TODO later
        }
    }
}