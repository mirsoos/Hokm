using Hokm.Application.Interfaces;
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
        public async Task<(bool Allowed, long Count, long RetryAfterMilliseconds)> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window)
        {
            const string script = """
                    local time = redis.call('TIME')
                    local now = tonumber(time[1]) * 1000
                        + math.floor(tonumber(time[2]) / 1000)

                    local window = tonumber(ARGV[2])
                    local maxRequests = tonumber(ARGV[3])
                    local cutoff = now - window

                    redis.call(
                        'ZREMRANGEBYSCORE',
                        KEYS[1],
                        '-inf',
                        cutoff
                    )

                    local count = redis.call('ZCARD', KEYS[1])

                    if count >= maxRequests then
                        local oldest = redis.call(
                            'ZRANGE',
                            KEYS[1],
                            0,
                            0,
                            'WITHSCORES'
                        )

                        local retryAfter =
                            tonumber(oldest[2]) + window - now

                        redis.call('PEXPIRE', KEYS[1], window)

                        return {0, count, math.max(0, retryAfter)}
                    end

                    redis.call('ZADD', KEYS[1], now, ARGV[1])

                    count = count + 1

                    redis.call('PEXPIRE', KEYS[1], window)

                    return {1, count, 0}
                    """;

            var windowMilliseconds = (long)window.TotalMilliseconds;

            var result = (RedisResult[])await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { key },
                new RedisValue[]
                {
            Guid.NewGuid().ToString("N"),
            windowMilliseconds,
            maxRequests
                });

            return (
                Allowed: (long)result[0] == 1,
                Count: (long)result[1],
                RetryAfterMilliseconds: (long)result[2]
            );
        }
    }
}
