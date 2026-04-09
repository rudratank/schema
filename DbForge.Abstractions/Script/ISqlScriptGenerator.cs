using DbForge.Core.Models.Schema;

namespace DbForge.Abstractions.Script
{
    public interface ISqlScriptGenerator
    {
        string GenerateTableScript ( TableDefinition table );
    }
}
