namespace DbForge.Core.Models.Schema
{
    public class ForeignKeyDefinition
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public string ReferencedTable { get; set; } = string.Empty;
        public List<string> ReferencedColumns { get; set; } = new();
        public string OnDelete { get; set; } = "NO ACTION";   // CASCADE, SET NULL, etc.
        public string OnUpdate { get; set; } = "NO ACTION";
    }
}
