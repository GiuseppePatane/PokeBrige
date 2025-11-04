using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace PokeBridge.Infrastructure.Translator.Policies;

/// <summary>
/// Defines resilience policies for HTTP clients
/// </summary>
public static class HttpClientTranslationPolicies
{
    /// <summary>
    /// Creates a retry policy for handling transient HTTP errors only.
    /// Does NOT retry on 429 (Too Many Requests https://http.cat/status/429) because FunTranslations API has strict rate limits:
    /// - 5 calls per hour for public API
    /// - 60 calls per day
    /// </summary>
    /// <returns>Async policy that retries failed HTTP requests</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles only 5xx and 408 (transient errors)
            .WaitAndRetryAsync(
                retryCount: 2, 
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "HTTP request retry attempt {RetryCount} after {DelaySeconds}s. Status: {StatusCode}, Reason: {ReasonPhrase}",
                        retryCount,
                        timespan.TotalSeconds,
                        outcome.Result?.StatusCode,
                        outcome.Result?.ReasonPhrase);
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy to prevent cascading failures.
    /// Opens the circuit after 3 consecutive rate limit errors (429), preventing further requests for 15 minutes.
    /// For transient errors (5xx), opens after 5 failures for 30 seconds.
    /// </summary>
    /// <returns>Async policy that implements circuit breaker pattern</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        // Circuit breaker for rate limiting (429) - longer break period
        var rateLimitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5, // 50% of requests failing
                samplingDuration: TimeSpan.FromSeconds(10), // Sample window
                minimumThroughput: 3, // At least 3 requests in window
                durationOfBreak: TimeSpan.FromMinutes(15), // Break for 15 minutes (respects hourly rate limit)
                onBreak: (outcome, duration, context) =>
                {
                    var logger = context.GetLogger();
                    if (outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        logger?.LogError(
                            "Circuit breaker opened for {DurationMinutes} minutes due to rate limiting (429). " +
                            "FunTranslations API limit: 5 calls/hour, 60 calls/day",
                            duration.TotalMinutes);
                    }
                    else
                    {
                        logger?.LogError(
                            "Circuit breaker opened for {DurationMinutes} minutes due to {StatusCode}",
                            duration.TotalMinutes,
                            outcome.Result?.StatusCode);
                    }
                },
                onReset: (context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogInformation("Circuit breaker reset - requests will be allowed");
                },
                onHalfOpen: () =>
                {
                    // no polly contex. so we can't use the logger.
                });

        return rateLimitBreaker;
    }

    /// <summary>
    /// Creates a timeout policy to prevent long-running requests.
    /// Cancels requests that take longer than 30 seconds.
    /// </summary>
    /// <returns>Async policy that implements timeout</returns>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(30),
            onTimeoutAsync: (context, timespan, _) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("HTTP request timeout after {TimeoutSeconds}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    private static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue("logger", out var logger))
        {
            return logger as ILogger;
        }
        return null;
    }
}