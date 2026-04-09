namespace DbForge.Core.Models.Compare
{
    public class CompareResult
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ComparedAt { get; set; } = DateTime.UtcNow;
        public string SourceDatabase { get; set; } = string.Empty;
        public string TargetDatabase { get; set; } = string.Empty;
        public CompareOptions Options { get; set; } = new();

        public List<DiffItem> DiffItems { get; set; } = new();
        public List<TableDiff> Tables { get; set; } = new();

        // Computed summaries for UI
        public int AddedCount => DiffItems.Count(d => d.DiffType == Enums.DiffType.Added);
        public int RemovedCount => DiffItems.Count(d => d.DiffType == Enums.DiffType.Removed);
        public int ModifiedCount => DiffItems.Count(d => d.DiffType == Enums.DiffType.Modified);
        public bool HasDifferences => DiffItems.Any(d => d.DiffType != Enums.DiffType.Identical);
    }
}
