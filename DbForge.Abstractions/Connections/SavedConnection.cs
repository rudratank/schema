namespace DbForge.Abstractions.Connections
{
    public class SavedConnection
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ConnectionProfile Profile { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
