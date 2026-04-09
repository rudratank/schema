using DbForge.Core.Models.Enums;

namespace DbForge.Core.Models.Schema
{
    public class SchemaModel
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string ServerVersion { get; set; } = string.Empty;
        public ProviderType ProviderType { get; set; }
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

        public List<TableDefinition> Tables { get; set; } = new();
        //public List<ViewDefinition> Views { get; set; } = new();
        //public List<ProcedureDefinition> Procedures { get; set; } = new();
        //public List<FunctionDefinition> Functions { get; set; } = new();

        // Helper — find a table by name quickly
        public TableDefinition? GetTable ( string name ) =>
            Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
