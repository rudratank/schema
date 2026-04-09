using DbForge.Abstractions.Connections;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DbForge.Infrastructure.Persistence;

public class SqliteConnectionRepository : IConnectionRepository
{
    private readonly AppDbContext _context;

    public SqliteConnectionRepository ( AppDbContext context )
    {
        _context = context;
    }

    public async Task SaveAsync ( SavedConnection connection )
    {
        var existing = await _context.SavedConnections.FindAsync(connection.Id);
        if ( existing is not null )
        {
            existing.Name = connection.Name;
            existing.ProfileJson = JsonSerializer.Serialize(connection.Profile);
        }
        else
        {
            _context.SavedConnections.Add(new SavedConnectionEntity
            {
                Id = connection.Id,
                Name = connection.Name,
                ProfileJson = JsonSerializer.Serialize(connection.Profile),
                CreatedAt = connection.CreatedAt
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<SavedConnection>> GetAllAsync ()
    {
        var rows = await _context.SavedConnections.OrderBy(e => e.Name).ToListAsync();
        return rows.Select(Map);
    }

    public async Task<SavedConnection?> GetByIdAsync ( Guid id )
    {
        var row = await _context.SavedConnections.FindAsync(id);
        return row is null ? null : Map(row);
    }

    public async Task DeleteAsync ( Guid id )
    {
        var row = await _context.SavedConnections.FindAsync(id);
        if ( row is not null )
        {
            _context.SavedConnections.Remove(row);
            await _context.SaveChangesAsync();
        }
    }

    private static SavedConnection Map ( SavedConnectionEntity e ) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Profile = JsonSerializer.Deserialize<ConnectionProfile>(e.ProfileJson) ?? new(),
        CreatedAt = e.CreatedAt
    };
}