namespace DbForge.Providers.SqlServer.Schema;

internal static class SqlServerSchemaQueries
{
    public const string GetTables = @"
        SELECT
            t.name  AS TABLE_NAME,
            s.name  AS TABLE_SCHEMA
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        ORDER BY t.name";

    /// <summary>
    /// Returns one row per column.
    /// Includes: precision, scale, max_length, is_primary_key, is_descending_key.
    /// is_descending_key is fetched from index_columns for the PK index.
    /// </summary>
    public const string GetColumns = @"
        SELECT
            t.name                  AS TABLE_NAME,
            c.name                  AS COLUMN_NAME,
            c.column_id             AS ORDINAL_POSITION,
            ty.name                 AS DATA_TYPE,
            c.max_length,
            c.precision,
            c.scale,
            c.is_nullable,
            c.is_identity,
            dc.definition           AS COLUMN_DEFAULT,
            CAST(
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.indexes i
                    JOIN sys.index_columns ic
                        ON i.object_id = ic.object_id
                       AND i.index_id  = ic.index_id
                    WHERE i.is_primary_key = 1
                      AND i.object_id      = c.object_id
                      AND ic.column_id     = c.column_id
                ) THEN 1 ELSE 0 END
            AS BIT)                 AS is_primary_key
        FROM sys.columns c
        JOIN sys.tables  t  ON c.object_id      = t.object_id
        JOIN sys.types   ty ON c.user_type_id   = ty.user_type_id
        LEFT JOIN sys.default_constraints dc
            ON c.default_object_id = dc.object_id
        ORDER BY t.name, c.column_id";

    /// <summary>
    /// Returns one row per index column (including PK).
    /// </summary>
    public const string GetIndexes = @"
        SELECT
            t.name          AS TABLE_NAME,
            i.name          AS INDEX_NAME,
            i.is_unique,
            i.is_primary_key,
            CAST(
                CASE i.type WHEN 1 THEN 1 ELSE 0 END
            AS BIT)         AS is_clustered,
            ic.key_ordinal,
            c.name          AS COLUMN_NAME,
            ic.is_descending_key
        FROM sys.indexes i
        JOIN sys.index_columns ic
            ON i.object_id = ic.object_id
           AND i.index_id  = ic.index_id
        JOIN sys.columns c
            ON ic.object_id  = c.object_id
           AND ic.column_id  = c.column_id
        JOIN sys.tables t ON i.object_id = t.object_id
        WHERE i.type > 0          -- exclude heap (type 0)
          AND ic.is_included_column = 0
        ORDER BY t.name, i.name, ic.key_ordinal";

    public const string GetForeignKeys = @"
        SELECT
            t.name   AS TABLE_NAME,
            fk.name  AS CONSTRAINT_NAME,
            c.name   AS COLUMN_NAME,
            rt.name  AS REFERENCED_TABLE_NAME,
            rc.name  AS REFERENCED_COLUMN_NAME,
            fk.delete_referential_action_desc,
            fk.update_referential_action_desc
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc
            ON fk.object_id = fkc.constraint_object_id
        JOIN sys.tables  t  ON fk.parent_object_id      = t.object_id
        JOIN sys.columns c
            ON fkc.parent_column_id = c.column_id
           AND c.object_id          = t.object_id
        JOIN sys.tables  rt ON fk.referenced_object_id  = rt.object_id
        JOIN sys.columns rc
            ON fkc.referenced_column_id = rc.column_id
           AND rc.object_id             = rt.object_id
        ORDER BY t.name, fk.name";
}