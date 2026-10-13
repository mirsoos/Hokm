
using Hokm.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Hokm.Presentation.gRPC.Middleware
{
    public class RateLimitMiddleware
    {
        private const int MaxRequests = 50;

        private static readonly TimeSpan Window =
            TimeSpan.FromSeconds(60);

        private readonly RequestDelegate _next;
        private readonly IRedisCacheService _cache;
        private readonly ILogger<RateLimitMiddleware> _logger;

        public RateLimitMiddleware(
            RequestDelegate next,
            IRedisCacheService cache,
            ILogger<RateLimitMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (path.StartsWith(
                    "/favicon",
                    StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(
                    "/swagger",
                    StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var identifier = GetIdentifier(context);
            var key = $"ratelimit:{identifier}";

            (bool Allowed, long Count, long RetryAfterMilliseconds) result;

            try
            {
                result = await _cache.CheckRateLimitAsync(
                    key,
                    MaxRequests,
                    Window);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Redis rate limiting failed for {Identifier}. " +
                    "Applying Fail Open policy.",
                    identifier);

                await _next(context);
                return;
            }

            context.Response.Headers["X-RateLimit-Limit"] =
                MaxRequests.ToString();

            context.Response.Headers["X-RateLimit-Remaining"] =
                Math.Max(0, MaxRequests - result.Count).ToString();

            if (!result.Allowed)
            {
                var retryAfterSeconds = Math.Max(
                    1,
                    (long)Math.Ceiling(
                        result.RetryAfterMilliseconds / 1000d));

                context.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                context.Response.Headers["Retry-After"] =
                    retryAfterSeconds.ToString();

                context.Response.ContentType =
                    "text/plain; charset=utf-8";

                await context.Response.WriteAsync(
                    "تعداد درخواست‌های شما بیش از حد مجاز است. لطفاً کمی صبر کنید.",
                    context.RequestAborted);

                return;
            }

            await _next(context);
        }

        private static string GetIdentifier(HttpContext context)
        {
            var userId = context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
                return $"u:{userId}";

            var remoteIp =
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            return $"ip:{remoteIp}";
        }
    }
}
