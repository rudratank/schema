namespace DbForge.Providers.MySql.Schema
{
    // All SQL in one place. Never inline SQL in logic files.
    internal static class MySqlSchemaQueries
    {
        public const string GetTables = @"
        SELECT
            TABLE_NAME,
            TABLE_SCHEMA,
            TABLE_COMMENT,
            ENGINE
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @DatabaseName
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME";

        public const string GetColumns = @"
        SELECT
            TABLE_NAME,
            COLUMN_NAME,
            ORDINAL_POSITION,
            COLUMN_DEFAULT,
            IS_NULLABLE,
            DATA_TYPE,
            COLUMN_TYPE,
            CHARACTER_MAXIMUM_LENGTH,
            NUMERIC_PRECISION,
            NUMERIC_SCALE,
            EXTRA,
            COLUMN_COMMENT,
            COLLATION_NAME,
            COLUMN_KEY
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @DatabaseName
        ORDER BY TABLE_NAME, ORDINAL_POSITION";

        public const string GetIndexes = @"
        SELECT
            TABLE_NAME,
            INDEX_NAME,
            NON_UNIQUE,
            SEQ_IN_INDEX,
            COLUMN_NAME,
            COLLATION
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = @DatabaseName
        ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX";

        public const string GetForeignKeys = @"
        SELECT
            kcu.TABLE_NAME,
            kcu.CONSTRAINT_NAME,
            kcu.COLUMN_NAME,
            kcu.REFERENCED_TABLE_NAME,
            kcu.REFERENCED_COLUMN_NAME,
            rc.DELETE_RULE,
            rc.UPDATE_RULE
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
            ON kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
            AND kcu.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA
        WHERE kcu.TABLE_SCHEMA = @DatabaseName
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
        ORDER BY kcu.TABLE_NAME, kcu.CONSTRAINT_NAME";
    }
}
