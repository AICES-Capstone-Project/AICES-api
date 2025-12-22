using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public interface IRedisHelper
    {
        Task<bool> PushJobAsync(string queueName, object jobData);
        Task<string?> PopJobAsync(string queueName);
    }
}

