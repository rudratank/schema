
namespace DbForge.Core.Models.Schema
{
    public class TableDefinition
    {
        public string Name { get; set; } = string.Empty;

        //dbo in sql,public in postgres, empty for mysql (which doesn't use schemas)
        public string SchemaName { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public string Engine { get; set; } = string.Empty;       // MySQL: InnoDB, MyISAM etc

        public List<ColumnDefinition> Columns { get; set; } = new();
        public List<IndexDefinition> Indexes { get; set; } = new();
        public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();

        //public string FullName => string.IsNullOrEmpty(SchemaName) ? Name : $"{SchemaName}.{Name}";
    }
}
