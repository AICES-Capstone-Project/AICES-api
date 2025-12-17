using Data.Entities;

namespace DataAccessLayer.IRepositories
{
    public interface IApplicationComparisonRepository
    {
        Task<ApplicationComparison> CreateAsync(ApplicationComparison applicationComparison);
    }
}
