namespace DbForge.Core.Models.Schema
{
    /// <summary>
    /// Represents a single stored procedure parameter, parsed from the
    /// procedure definition text.
    /// </summary>
    public class ProcParam
    {
        public string Name { get; init; } = string.Empty;
        public string DataType { get; init; } = string.Empty;
        public string? DefaultValue { get; init; }
        public bool IsOutput { get; init; }
    }
}