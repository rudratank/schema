using DbForge.Core.Models.Enums;

namespace DbForge.Core.Models.Compare
{
    public class DiffItem
    {
        public ObjectType ObjectType { get; set; }
        public string ObjectName { get; set; } = string.Empty;
        public string? ParentName { get; set; }           // for columns: the table name
        public DiffType DiffType { get; set; }
        public object? SourceDefinition { get; set; }     // the actual source object
        public object? TargetDefinition { get; set; }     // the actual target object
        public List<string> ChangedProperties { get; set; } = new();  // e.g. ["DataType", "IsNullable"]
    }
}
