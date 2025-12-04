using API.Data;

namespace API.Services
{
    public interface IDatabaseSelector
    {
        /// <summary>
        /// Get the current active database context
        /// </summary>
        StoreContext GetCurrentContext();

        /// <summary>
        /// Get all database contexts for searching across all databases
        /// </summary>
        IEnumerable<StoreContext> GetAllContexts();

        /// <summary>
        /// Check current database size and rotate if needed
        /// </summary>
        Task CheckAndRotateIfNeededAsync();

        /// <summary>
        /// Manually rotate to the next database
        /// </summary>
        Task RotateToNextDatabaseAsync();

        /// <summary>
        /// Get the current database name
        /// </summary>
        string GetCurrentDatabaseName();
    }
}
