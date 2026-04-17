namespace DbForge.Core.Models.Schema
{
    public class SynonymDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string BaseObjectName { get; set; } = string.Empty;
    }
}