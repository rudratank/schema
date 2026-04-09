namespace DbForge.Infrastructure.Persistence;

// Flat DB row — profile serialized to JSON to avoid EF mapping complexity
public class SavedConnectionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProfileJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}