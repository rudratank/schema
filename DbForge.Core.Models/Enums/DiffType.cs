namespace DbForge.Core.Models.Enums
{
    public enum DiffType
    {
        Identical = 0,
        Added = 1,      // exists in target, not in source
        Removed = 2,    // exists in source, not in target
        Modified = 3    // exists in both but different
    }
}
