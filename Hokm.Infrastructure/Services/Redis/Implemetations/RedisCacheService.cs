using Hokm.Infrastructure.Services.Redis.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Hokm.Infrastructure.Services.Redis.Implemetations
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase _db;
        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            RedisValue value = await _db.StringGetAsync(key);

            if (value.IsNull)
                return default;

            string json = value.ToString();
            return JsonSerializer.Deserialize<T>(json);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(value);
            Expiration expiration = expiry.HasValue ? expiry.Value : Expiration.Default;
            await _db.StringSetAsync(key, json, expiration);
        }
    }
}
