namespace Hokm.Application.Interfaces
{
    public interface IRedisCacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken);
        Task RemoveAsync(string key, CancellationToken cancellationToken);
        Task<(bool Allowed, long Count, long RetryAfterMilliseconds)>CheckRateLimitAsync(string key,int maxRequests,TimeSpan window);
    }
}
