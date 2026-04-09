using DbForge.Abstractions.Compare;
using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare;

/// <summary>
/// Fixed version: Added/Removed tables now emit DiffItems for every column,
/// so they appear fully in the result tree (not just the TableDiff list).
/// </summary>
public class SchemaComparer : ISchemaComparer
{
    private readonly TableComparer _tableComparer = new();
    private readonly ColumnComparer _columnComparer = new();
    private readonly IndexComparer _indexComparer = new();
    private readonly ForeignKeyComparer _fkComparer = new();
    private readonly PrimaryKeyComparer _pkComparer = new();

    public CompareResult Compare ( SchemaModel source, SchemaModel target )
    {
        var result = new CompareResult
        {
            SourceDatabase = source.DatabaseName,
            TargetDatabase = target.DatabaseName
        };

        var sourceMap = source.Tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var targetMap = target.Tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        // ── 1. Table-level diff list (Added / Removed / Modified) ──────────
        result.Tables = _tableComparer.Compare(source.Tables, target.Tables);

        // ── 2. Tables only in source → Removed ─────────────────────────────
        foreach ( var (name, srcTable) in sourceMap )
        {
            if ( targetMap.ContainsKey(name) ) continue;

            // Emit one DiffItem per column so the tree can show them
            foreach ( var col in srcTable.Columns )
            {
                result.DiffItems.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = col.Name,
                    ParentName = srcTable.Name,
                    DiffType = DiffType.Removed,
                    SourceDefinition = col
                });
            }
        }

        // ── 3. Tables only in target → Added ───────────────────────────────
        foreach ( var (name, tgtTable) in targetMap )
        {
            if ( sourceMap.ContainsKey(name) ) continue;

            foreach ( var col in tgtTable.Columns )
            {
                result.DiffItems.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = col.Name,
                    ParentName = tgtTable.Name,
                    DiffType = DiffType.Added,
                    TargetDefinition = col
                });
            }
        }

        // ── 4. Tables in both → deep compare ───────────────────────────────
        foreach ( var (name, srcTable) in sourceMap )
        {
            if ( !targetMap.TryGetValue(name, out var tgtTable) ) continue;

            result.DiffItems.AddRange(_columnComparer.Compare(srcTable, tgtTable));
            result.DiffItems.AddRange(_pkComparer.Compare(srcTable, tgtTable));
            result.DiffItems.AddRange(_indexComparer.Compare(srcTable, tgtTable));
            result.DiffItems.AddRange(_fkComparer.Compare(srcTable, tgtTable));
        }

        return result;
    }
}