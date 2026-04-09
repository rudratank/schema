namespace DbForge.Core.Models.Schema
{
    public class IndexDefinition
    {
        public string Name { get; set; } = string.Empty;
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsClustered { get; set; }    // SQL Server specific, ignored elsewhere
        public List<IndexColumn> Columns { get; set; } = new();
    }

    public class IndexColumn
    {
        public string ColumnName { get; set; } = string.Empty;
        public bool Descending { get; set; }
        public int Position { get; set; }
    }
}
