using DbForge.Abstractions.Compare;
using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Orchestrates the full schema diff across all supported object types.
    ///
    /// Object types compared
    /// ─────────────────────
    ///  Tables      → TableComparer (summary) + deep comparers below
    ///  Columns     → ColumnComparer (per shared table)
    ///  Primary Keys→ PrimaryKeyComparer (per shared table)
    ///  Indexes     → IndexComparer (per shared table, non-PK only)
    ///  Foreign Keys→ ForeignKeyComparer (per shared table)
    ///  Procedures  → ProcedureComparer
    ///  Views       → ViewComparer
    ///  Functions   → FunctionComparer
    ///  Triggers    → TriggerComparer
    ///  Synonyms    → SynonymComparer
    ///
    /// Design notes
    /// ────────────
    ///  • Procedure/View/Function/Trigger/Synonym comparers run ONCE at the
    ///    top level, never inside the per-table loop.
    ///  • Column/PK/Index/FK diffs are only emitted for tables that exist in
    ///    BOTH schemas. Columns inside added/removed tables are represented
    ///    through the table-level DiffItems instead of individual column diffs.
    /// </summary>
    public class SchemaComparer : ISchemaComparer
    {
        private readonly TableComparer _tableComparer = new();
        private readonly ColumnComparer _columnComparer = new();
        private readonly IndexComparer _indexComparer = new();
        private readonly ForeignKeyComparer _fkComparer = new();
        private readonly PrimaryKeyComparer _pkComparer = new();
        private readonly ProcedureComparer _procedureComparer = new();
        private readonly ViewComparer _viewComparer = new();
        private readonly FunctionComparer _functionComparer = new();
        private readonly TriggerComparer _triggerComparer = new();
        private readonly SynonymComparer _synonymComparer = new();

        public CompareResult Compare ( SchemaModel source, SchemaModel target )
        {
            var result = new CompareResult
            {
                SourceDatabase = source.DatabaseName,
                TargetDatabase = target.DatabaseName
            };

            var sourceMap = source.Tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            var targetMap = target.Tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            // ── 1. Table-level summary ─────────────────────────────────────────
            result.Tables = _tableComparer.Compare(source.Tables, target.Tables);

            // ── 2. Added tables — emit table-level DiffItems only ──────────────
            //    Individual column diffs for brand-new tables are not emitted;
            //    the Added table entry is sufficient for script generation.
            foreach ( var (name, tgtTable) in targetMap )
            {
                if ( !sourceMap.ContainsKey(name) )
                {
                    result.DiffItems.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Table,
                        ObjectName = name,
                        ParentName = tgtTable.SchemaName,
                        DiffType = DiffType.Added,
                        TargetDefinition = tgtTable
                    });
                }
            }

            // ── 3. Removed tables — emit table-level DiffItems only ────────────
            foreach ( var (name, srcTable) in sourceMap )
            {
                if ( !targetMap.ContainsKey(name) )
                {
                    result.DiffItems.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Table,
                        ObjectName = name,
                        ParentName = srcTable.SchemaName,
                        DiffType = DiffType.Removed,
                        SourceDefinition = srcTable
                    });
                }
            }

            // ── 4. Tables in both → deep compare ──────────────────────────────
            foreach ( var (name, srcTable) in sourceMap )
            {
                if ( !targetMap.TryGetValue(name, out var tgtTable) )
                    continue;

                result.DiffItems.AddRange(_columnComparer.Compare(srcTable, tgtTable));
                result.DiffItems.AddRange(_pkComparer.Compare(srcTable, tgtTable));
                result.DiffItems.AddRange(_indexComparer.Compare(srcTable, tgtTable));
                result.DiffItems.AddRange(_fkComparer.Compare(srcTable, tgtTable));
            }

            // ── 5. Procedures ─────────────────────────────────────────────────
            result.DiffItems.AddRange(
                _procedureComparer.Compare(source.Procedures, target.Procedures));

            // ── 6. Views ──────────────────────────────────────────────────────
            result.DiffItems.AddRange(
                _viewComparer.Compare(source.Views, target.Views));

            // ── 7. Functions ──────────────────────────────────────────────────
            result.DiffItems.AddRange(
                _functionComparer.Compare(source.Functions, target.Functions));

            // ── 8. Triggers ───────────────────────────────────────────────────
            result.DiffItems.AddRange(
                _triggerComparer.Compare(source.Triggers, target.Triggers));

            // ── 9. Synonyms ───────────────────────────────────────────────────
            result.DiffItems.AddRange(
                _synonymComparer.Compare(source.Synonyms, target.Synonyms));

            return result;
        }
    }
}