using DbForge.Core.Models.Compare;
using DbForge.Core.Models.Enums;
using DbForge.Core.Models.Schema;

namespace DbForge.Core.Compare
{
    /// <summary>
    /// Compares columns between two versions of the same table.
    ///
    /// Pipeline
    /// ────────
    ///  1. Exact name match → property diff → Modified or skip
    ///  2. Rename detection on unmatched columns via scoring (threshold ≥ 5)
    ///  3. Remaining unmatched source → Removed
    ///  4. Remaining unmatched target → Added
    ///
    /// Rename scoring
    /// ──────────────
    ///  +3  Same base DataType (most reliable signal — int stays int)
    ///  +2  Same FullDataType (varchar(255) == varchar(255))
    ///  +1  Same IsNullable
    ///  +1  Same IsIdentity
    ///  +1  CharacterMaxLength within ±0 (exact match preferred)
    ///  +3  Name similarity (one name contains the other, case-insensitive)
    ///  ──
    ///  Max: 11  |  Threshold: 5
    ///
    /// This avoids false renames between columns that share only nullability
    /// or between two unrelated int columns in the same table.
    /// </summary>
    public class ColumnComparer
    {
        private const int RenameScoreThreshold = 5;

        public List<DiffItem> Compare ( TableDefinition source, TableDefinition target )
        {
            var diffs = new List<DiffItem>();

            var srcMap = source.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            var tgtMap = target.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            // ── 1. Exact name match → property diff ─────────────────────────
            foreach ( var (name, srcCol) in srcMap )
            {
                if ( !tgtMap.TryGetValue(name, out var tgtCol) )
                    continue;

                var changes = GetChangedProperties(srcCol, tgtCol);
                if ( changes.Count > 0 )
                {
                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Column,
                        ObjectName = name,
                        ParentName = source.Name,
                        DiffType = DiffType.Modified,
                        SourceDefinition = srcCol,
                        TargetDefinition = tgtCol,
                        ChangedProperties = changes
                    });
                }
            }

            // ── Collect unmatched columns for rename detection ───────────────
            var unmatchedSrc = srcMap
                .Where(kv => !tgtMap.ContainsKey(kv.Key))
                .Select(kv => kv.Value)
                .ToList();

            var unmatchedTgt = tgtMap
                .Where(kv => !srcMap.ContainsKey(kv.Key))
                .Select(kv => kv.Value)
                .ToList();

            // ── 2. Rename detection ──────────────────────────────────────────
            var usedTgt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var srcCol in unmatchedSrc.ToList() )
            {
                // Find the best-scoring candidate in unmatched target columns
                var best = unmatchedTgt
                    .Where(t => !usedTgt.Contains(t.Name))
                    .Select(t => new { Col = t, Score = RenameScore(srcCol, t) })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if ( best != null && best.Score >= RenameScoreThreshold )
                {
                    var changes = new List<string> { $"Renamed: {srcCol.Name} → {best.Col.Name}" };

                    // Also report any property changes on the renamed column
                    var propChanges = GetChangedProperties(srcCol, best.Col);
                    if ( propChanges.Count > 0 )
                        changes.AddRange(propChanges);

                    diffs.Add(new DiffItem
                    {
                        ObjectType = ObjectType.Column,
                        ObjectName = $"{srcCol.Name} → {best.Col.Name}",
                        ParentName = source.Name,
                        DiffType = DiffType.Modified,
                        SourceDefinition = srcCol,
                        TargetDefinition = best.Col,
                        ChangedProperties = changes
                    });

                    usedTgt.Add(best.Col.Name);
                    unmatchedSrc.Remove(srcCol);
                    unmatchedTgt.Remove(best.Col);
                }
            }

            // ── 3. True Removed ──────────────────────────────────────────────
            foreach ( var col in unmatchedSrc )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = col.Name,
                    ParentName = source.Name,
                    DiffType = DiffType.Removed,
                    SourceDefinition = col
                });
            }

            // ── 4. True Added ────────────────────────────────────────────────
            foreach ( var col in unmatchedTgt.Where(c => !usedTgt.Contains(c.Name)) )
            {
                diffs.Add(new DiffItem
                {
                    ObjectType = ObjectType.Column,
                    ObjectName = col.Name,
                    ParentName = source.Name,
                    DiffType = DiffType.Added,
                    TargetDefinition = col
                });
            }

            return diffs;
        }

        // ════════════════════════════════════════════════════════════════════
        // RENAME SCORING
        // ════════════════════════════════════════════════════════════════════

        private static int RenameScore ( ColumnDefinition a, ColumnDefinition b )
        {
            int score = 0;

            // Base type match — strongest structural signal
            if ( string.Equals(a.DataType, b.DataType, StringComparison.OrdinalIgnoreCase) )
                score += 3;

            // Full type match (includes length/precision) — extra confidence
            if ( string.Equals(a.FullDataType, b.FullDataType, StringComparison.OrdinalIgnoreCase) )
                score += 2;

            // Nullability match
            if ( a.IsNullable == b.IsNullable )
                score += 1;

            // Identity match (auto-increment columns are very specific)
            if ( a.IsIdentity == b.IsIdentity )
                score += 1;

            // Exact character length match (not approximate — approximate causes false positives)
            if ( a.CharacterMaxLength.HasValue && b.CharacterMaxLength.HasValue
                && a.CharacterMaxLength == b.CharacterMaxLength )
                score += 1;

            // Name similarity (substring containment, case-insensitive)
            if ( IsNameSimilar(a.Name, b.Name) )
                score += 3;

            return score;
        }

        /// <summary>
        /// Returns true if one name is a substring of the other.
        /// Avoids matching completely unrelated short names (e.g. "Id" vs "ModifiedById").
        /// Requires the shorter name to be at least 3 characters to avoid noise.
        /// </summary>
        private static bool IsNameSimilar ( string a, string b )
        {
            var al = a.ToLowerInvariant();
            var bl = b.ToLowerInvariant();

            if ( al.Length < 3 || bl.Length < 3 )
                return false; // too short — too much noise risk

            return al.Contains(bl) || bl.Contains(al);
        }

        // ════════════════════════════════════════════════════════════════════
        // PROPERTY DIFF
        // ════════════════════════════════════════════════════════════════════

        private static List<string> GetChangedProperties ( ColumnDefinition a, ColumnDefinition b )
        {
            var changes = new List<string>();

            if ( !string.Equals(a.DataType, b.DataType, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"DataType: {a.DataType} → {b.DataType}");

            if ( !string.Equals(a.FullDataType, b.FullDataType, StringComparison.OrdinalIgnoreCase) )
                changes.Add($"FullDataType: {a.FullDataType} → {b.FullDataType}");

            if ( a.IsNullable != b.IsNullable )
                changes.Add($"IsNullable: {a.IsNullable} → {b.IsNullable}");

            if ( !string.Equals(a.DefaultValue ?? "", b.DefaultValue ?? "", StringComparison.OrdinalIgnoreCase) )
                changes.Add($"DefaultValue: '{a.DefaultValue ?? "none"}' → '{b.DefaultValue ?? "none"}'");

            if ( a.IsIdentity != b.IsIdentity )
                changes.Add($"Identity: {a.IsIdentity} → {b.IsIdentity}");

            if ( a.CharacterMaxLength != b.CharacterMaxLength )
                changes.Add($"MaxLength: {a.CharacterMaxLength?.ToString() ?? "null"} → {b.CharacterMaxLength?.ToString() ?? "null"}");

            if ( a.NumericPrecision != b.NumericPrecision )
                changes.Add($"Precision: {a.NumericPrecision?.ToString() ?? "null"} → {b.NumericPrecision?.ToString() ?? "null"}");

            if ( a.NumericScale != b.NumericScale )
                changes.Add($"Scale: {a.NumericScale?.ToString() ?? "null"} → {b.NumericScale?.ToString() ?? "null"}");

            if ( !string.Equals(a.Collation ?? "", b.Collation ?? "", StringComparison.OrdinalIgnoreCase) )
                changes.Add($"Collation: '{a.Collation ?? "none"}' → '{b.Collation ?? "none"}'");

            return changes;
        }
    }
}