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

        /// <summary>
        /// Store job data with a specific key (for tracking and retrieval)
        /// </summary>
        // public async Task<bool> SetJobDataAsync(string key, object jobData, TimeSpan? expiry = null)
        // {
        //     try
        //     {
        //         var json = JsonSerializer.Serialize(jobData);
        //         await _database.StringSetAsync(key, json, expiry);
        //         return true;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Error setting job data in Redis: {ex.Message}");
        //         return false;
        //     }
        // }

        // /// <summary>
        // /// Get job data by key
        // /// </summary>
        // public async Task<T?> GetJobDataAsync<T>(string key)
        // {
        //     try
        //     {
        //         var json = await _database.StringGetAsync(key);
        //         if (json.IsNullOrEmpty)
        //             return default;

        //         return JsonSerializer.Deserialize<T>(json!);
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Error getting job data from Redis: {ex.Message}");
        //         return default;
        //     }
        // }

        // /// <summary>
        // /// Delete job data by key
        // /// </summary>
        // public async Task<bool> DeleteJobDataAsync(string key)
        // {
        //     try
        //     {
        //         await _database.KeyDeleteAsync(key);
        //         return true;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Error deleting job data from Redis: {ex.Message}");
        //         return false;
        //     }
        // }
    }
}

