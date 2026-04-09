namespace DbForge.Core.Models.Schema
{
    public class ColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public int OrdinalPosition { get; set; }
        // "varchar", "int", "timestamp"
        public string DataType { get; set; } = string.Empty;
        // "varchar(255)", "decimal(10,2)"
        public string FullDataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }
        // auto_increment / IDENTITY
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int? CharacterMaxLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public string? Comment { get; set; }
        public string? Collation { get; set; }
    }
}
