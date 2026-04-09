namespace DbForge.Core.Models.Compare
{
    public class CompareOptions
    {
        public bool IgnoreCase { get; set; } = true;
        public bool IgnoreWhitespace { get; set; } = true;
        public bool IgnoreComments { get; set; } = true;
        public bool CompareTables { get; set; } = true;
        public bool CompareViews { get; set; } = true;
        public bool CompareProcedures { get; set; } = true;
        public bool CompareIndexes { get; set; } = true;
        public bool CompareForeignKeys { get; set; } = true;
        public List<string> ExcludeObjects { get; set; } = new();  // object names to skip
    }
}
