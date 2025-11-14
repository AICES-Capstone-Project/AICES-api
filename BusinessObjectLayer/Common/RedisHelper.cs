using StackExchange.Redis;
using System.Text.Json;

namespace BusinessObjectLayer.Common
{
    public class RedisHelper
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public RedisHelper(IConnectionMultiplexer redis)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _database = _redis.GetDatabase();
        }

        /// <summary>
        /// Push a job to Redis queue
        /// </summary>
        public async Task<bool> PushJobAsync(string queueName, object jobData)
        {
            try
            {
                var json = JsonSerializer.Serialize(jobData);
                await _database.ListRightPushAsync(queueName, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pushing job to Redis: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get a job from Redis queue
        /// </summary>
        public async Task<T?> PopJobAsync<T>(string queueName)
        {
            try
            {
                var json = await _database.ListLeftPopAsync(queueName);
                if (json.IsNullOrEmpty)
                    return default;

                return JsonSerializer.Deserialize<T>(json!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error popping job from Redis: {ex.Message}");
                return default;
            }
        }
    }
}

