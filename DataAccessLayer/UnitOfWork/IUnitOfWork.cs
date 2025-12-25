using System;
using System.Threading.Tasks;

namespace DataAccessLayer.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Exposes DbContext for advanced queries
        /// </summary>
        AICESDbContext Context { get; }
        
        /// <summary>
        /// Begins a new database transaction
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commits the current transaction and saves all changes to the database
        /// </summary>
        Task CommitTransactionAsync();

        /// <summary>
        /// Rolls back the current transaction
        /// </summary>
        Task RollbackTransactionAsync();

        /// <summary>
        /// Saves changes to the database without committing the transaction
        /// Used for intermediate saves to generate IDs for FK relationships
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Gets a repository instance for the specified type
        /// </summary>
        /// <typeparam name="TRepository">The repository interface type</typeparam>
        /// <returns>An instance of the requested repository</returns>
        TRepository GetRepository<TRepository>() where TRepository : class;
    }
}

