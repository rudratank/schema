namespace DbForge.Core.Models.Schema
{
    /// <summary>Maps sys.objects.type for user-defined functions.</summary>
    public enum SqlFunctionType
    {
        Scalar,                    // FN
        InlineTableValued,         // IF
        MultiStatementTableValued  // TF
    }

    public class FunctionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string Definition { get; set; } = string.Empty;

        /// <summary>Scalar | InlineTableValued | MultiStatementTableValued.</summary>
        public SqlFunctionType FunctionType { get; set; }

        /// <summary>
        /// Normalised text of the RETURNS clause, e.g. "int", "TABLE", "TABLE (Id int, Name varchar(100))".
        /// Populated by FunctionComparer.ExtractReturnType().
        /// </summary>
        public string ReturnType { get; set; } = string.Empty;
    }
}