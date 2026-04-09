using DbForge.Abstractions.Script;
using DbForge.Core.Models.Schema;
using System.Text;

namespace DbForge.Providers.SqlServer.Script
{
    public class SqlScriptGenerator : ISqlScriptGenerator
    {
        public string GenerateTableScript ( TableDefinition table )
        {
            if ( table == null )
                return "-- Table does not exist";

            var sb = new StringBuilder();

            sb.AppendLine($"CREATE TABLE [{table.SchemaName}].[{table.Name}]");
            sb.AppendLine("(");

            var columns = table.Columns
                .OrderBy(c => c.OrdinalPosition)
                .Select(c =>
                {
                    var line = $"    [{c.Name}] {c.FullDataType}";

                    line += c.IsNullable ? " NULL" : " NOT NULL";

                    if ( c.IsIdentity )
                        line += " IDENTITY(1,1)";

                    if ( !string.IsNullOrEmpty(c.DefaultValue) )
                        line += $" DEFAULT {c.DefaultValue}";

                    return line;
                });

            sb.AppendLine(string.Join(",\n", columns));
            sb.AppendLine(")");
            sb.AppendLine("GO");

            // Primary Key
            var pk = table.Indexes.FirstOrDefault(i => i.IsPrimaryKey);
            if ( pk != null )
            {
                var cols = string.Join(",",
                    pk.Columns.OrderBy(c => c.Position)
                              .Select(c => $"[{c.ColumnName}]"));

                sb.AppendLine($"ALTER TABLE [{table.Name}] ADD CONSTRAINT [{pk.Name}] PRIMARY KEY ({cols});");
                sb.AppendLine("GO");
            }

            // Foreign Keys
            foreach ( var fk in table.ForeignKeys )
            {
                sb.AppendLine(
                    $"ALTER TABLE [{table.Name}] ADD CONSTRAINT [{fk.Name}] " +
                    $"FOREIGN KEY ({string.Join(",", fk.Columns)}) " +
                    $"REFERENCES [{fk.ReferencedTable}] ({string.Join(",", fk.ReferencedColumns)});"
                );
                sb.AppendLine("GO");
            }

            return sb.ToString();
        }
    }
}
