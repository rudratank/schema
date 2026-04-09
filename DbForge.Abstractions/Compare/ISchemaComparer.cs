using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Schema;


namespace DbForge.Abstractions.Compare
{
    public interface ISchemaComparer
    {
        CompareResult Compare ( SchemaModel source, SchemaModel target );
    }
}
