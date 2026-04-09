namespace DbForge.Abstractions.Connections
{
    public interface IConnectionRepository
    {
        Task SaveAsync ( SavedConnection connection );
        Task<IEnumerable<SavedConnection>> GetAllAsync ();
        Task<SavedConnection?> GetByIdAsync ( Guid id );
        Task DeleteAsync ( Guid id );
    }
}
