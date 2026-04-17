namespace DbForge.Core.Models.Schema
{
    public class ViewDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string Definition { get; set; } = string.Empty;
        public bool IsIndexed { get; set; }
        public bool IsSchemaBound { get; set; }
    }
}