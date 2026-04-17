namespace DbForge.Providers.SqlServer.Schema;

internal static class SqlServerSchemaQueries
{
    // ── Existing queries (unchanged) ─────────────────────────────────────────

    public const string GetTables = @"
        SELECT
            t.name  AS TABLE_NAME,
            s.name  AS TABLE_SCHEMA
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        ORDER BY t.name";

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
        WHERE i.type > 0
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

    public const string GetProcedures = @"
        SELECT
            p.name          AS PROCEDURE_NAME,
            s.name          AS PROCEDURE_SCHEMA,
            m.definition    AS PROCEDURE_DEFINITION
        FROM sys.procedures p
        JOIN sys.sql_modules m ON p.object_id = m.object_id
        JOIN sys.schemas s ON p.schema_id = s.schema_id
        ORDER BY p.name";

    // ── NEW: Views ────────────────────────────────────────────────────────────
    /// <summary>
    /// All user-defined views with their full definition text.
    /// is_schema_bound comes from sys.sql_modules.
    /// is_indexed is derived by checking for a clustered index on the view.
    /// </summary>
    public const string GetViews = @"
        SELECT
            v.name              AS VIEW_NAME,
            s.name              AS VIEW_SCHEMA,
            m.definition        AS VIEW_DEFINITION,
            m.is_schema_bound,
            CAST(
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.indexes i
                    WHERE  i.object_id = v.object_id
                      AND  i.type      = 1          -- CLUSTERED
                      AND  i.is_primary_key = 0
                ) THEN 1 ELSE 0 END
            AS BIT)             AS is_indexed
        FROM sys.views       v
        JOIN sys.sql_modules m  ON v.object_id = m.object_id
        JOIN sys.schemas     s  ON v.schema_id = s.schema_id
        WHERE v.is_ms_shipped = 0
        ORDER BY v.name";

    // ── NEW: Functions ────────────────────────────────────────────────────────
    /// <summary>
    /// All user-defined scalar (FN), inline table-valued (IF) and
    /// multi-statement table-valued (TF) functions.
    /// </summary>
    public const string GetFunctions = @"
        SELECT
            o.name          AS FUNCTION_NAME,
            s.name          AS FUNCTION_SCHEMA,
            m.definition    AS FUNCTION_DEFINITION,
            o.type          AS FUNCTION_TYPE
        FROM sys.objects     o
        JOIN sys.sql_modules m  ON o.object_id = m.object_id
        JOIN sys.schemas     s  ON o.schema_id = s.schema_id
        WHERE o.type          IN ('FN', 'IF', 'TF')
          AND o.is_ms_shipped  = 0
        ORDER BY o.name";

    // ── NEW: Triggers ─────────────────────────────────────────────────────────
    /// <summary>
    /// All user-defined DML triggers (parent_class = 1).
    /// fires_on_insert / update / delete are derived from sys.trigger_events.
    /// </summary>
    public const string GetTriggers = @"
        SELECT
            tr.name                             AS TRIGGER_NAME,
            s.name                              AS TRIGGER_SCHEMA,
            m.definition                        AS TRIGGER_DEFINITION,
            OBJECT_NAME(tr.parent_id)           AS PARENT_TABLE,
            tr.is_disabled,
            tr.is_instead_of_trigger,
            CAST(
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.trigger_events te
                    WHERE te.object_id = tr.object_id AND te.type = 1
                ) THEN 1 ELSE 0 END AS BIT)     AS fires_on_insert,
            CAST(
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.trigger_events te
                    WHERE te.object_id = tr.object_id AND te.type = 2
                ) THEN 1 ELSE 0 END AS BIT)     AS fires_on_update,
            CAST(
                CASE WHEN EXISTS (
                    SELECT 1 FROM sys.trigger_events te
                    WHERE te.object_id = tr.object_id AND te.type = 3
                ) THEN 1 ELSE 0 END AS BIT)     AS fires_on_delete
        FROM sys.triggers    tr
        JOIN sys.sql_modules m  ON tr.object_id = m.object_id
        JOIN sys.objects     po ON tr.parent_id  = po.object_id
        JOIN sys.schemas     s  ON po.schema_id  = s.schema_id
        WHERE tr.is_ms_shipped  = 0
          AND tr.parent_class   = 1              -- DML triggers only
        ORDER BY OBJECT_NAME(tr.parent_id), tr.name";

    // ── NEW: Synonyms ─────────────────────────────────────────────────────────
    /// <summary>All synonyms with their fully-qualified base object reference.</summary>
    public const string GetSynonyms = @"
        SELECT
            sn.name             AS SYNONYM_NAME,
            s.name              AS SYNONYM_SCHEMA,
            sn.base_object_name AS BASE_OBJECT_NAME
        FROM sys.synonyms sn
        JOIN sys.schemas  s  ON sn.schema_id = s.schema_id
        ORDER BY sn.name";
}