namespace DbForge.Core.Models.Schema
{
    [Flags]
    public enum TriggerEvents
    {
        None = 0,
        Insert = 1,
        Update = 2,
        Delete = 4
    }

    public enum TriggerTiming
    {
        After,     // AFTER / FOR
        InsteadOf  // INSTEAD OF
    }

    public class TriggerDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string Definition { get; set; } = string.Empty;

        /// <summary>The table or view this trigger fires on.</summary>
        public string ParentTable { get; set; } = string.Empty;

        /// <summary>Bitfield of INSERT | UPDATE | DELETE.</summary>
        public TriggerEvents Events { get; set; }

        /// <summary>AFTER or INSTEAD OF.</summary>
        public TriggerTiming Timing { get; set; }

        /// <summary>False means the trigger exists but is currently disabled.</summary>
        public bool IsEnabled { get; set; } = true;
    }
}