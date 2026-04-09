namespace DbForge.Core.Models.Compare
{
    public class TableDiff
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Added / Removed / Modified
        public string Owner { get; set; } = "dbo";
    }
}
