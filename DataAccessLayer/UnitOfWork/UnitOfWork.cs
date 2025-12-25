using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace DataAccessLayer.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        //EF DbContext — đại diện cho 1 “unit” làm việc với DB.
        private readonly AICESDbContext _context;
        //DI container để resolve repository đúng interface.
        private readonly IServiceProvider _serviceProvider;
        //object quản lý transaction hiện tại.
        private IDbContextTransaction? _transaction;

        //Constructor nhận vào DbContext và IServiceProvider.
        //Lấy DB + DI container từ .NET.
        //UoW sống trong 1 request scope, giống như repository.
        //Vì vậy nó phải được resolve từ DI container.
        //Và nó phải được dispose khi request kết thúc.
        /// <summary>
        /// Exposes DbContext for advanced queries
        /// </summary>
        public AICESDbContext Context => _context;
        
        public UnitOfWork(AICESDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        //Mở transaction mới
        //Nếu đang mở mà gọi lại → throw lỗi
        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("A transaction is already in progress.");
            }

            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction is in progress.");
            }

            try
            {
                await _context.SaveChangesAsync();
                await _transaction.CommitAsync();
            }
            catch
            {
                await _transaction.RollbackAsync();
                throw;
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction is in progress.");
            }

            try
            {
                await _transaction.RollbackAsync();
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public TRepository GetRepository<TRepository>() where TRepository : class
        {
            return _serviceProvider.GetRequiredService<TRepository>();
        }

        public void Dispose()
        {
            _transaction?.Dispose();
        }
    }
}

