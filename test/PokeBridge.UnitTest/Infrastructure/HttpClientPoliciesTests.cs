using System.Net;
using Moq;
using Moq.Protected;
using PokeBridge.Infrastructure.Translator.Policies;
using Shouldly;

namespace PokeBridge.UnitTest.Infrastructure;

public class HttpClientPoliciesTests
{
    [Fact]
    public async Task RetryPolicy_ShouldRetry_OnTransientHttpError()
    {
        // Arrange
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                // First 2 calls fail with 503, third succeeds
                return callCount <= 2
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("success") };
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var retryPolicy = HttpClientTranslationPolicies.GetRetryPolicy();

        // Act
        var response = await retryPolicy.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(3,"1 initial + 2 retries"); 
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_OnRateLimitError()
    {
        // Arrange
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("Rate limit exceeded")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var retryPolicy = HttpClientTranslationPolicies.GetRetryPolicy();

        // Act
        var response = await retryPolicy.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        callCount.ShouldBe(1,"No retries on 429"); 
    }

    [Fact]
    public async Task RetryPolicy_ShouldStopRetrying_AfterMaxAttempts()
    {
        // Arrange
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var retryPolicy = HttpClientTranslationPolicies.GetRetryPolicy();

        // Act
        var response = await retryPolicy.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        callCount.ShouldBe(3,"1 initial + 2 retries (max)"); 
    }

    [Fact]
    public async Task CircuitBreaker_ShouldAllowSuccessfulRequests()
    {
        // Arrange
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Success")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var circuitBreaker = HttpClientTranslationPolicies.GetCircuitBreakerPolicy();

        // Act
        var response = await circuitBreaker.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(1,"no breaker triggered");
    }

    [Fact]
    public async Task CircuitBreaker_ShouldHandleRateLimitErrors()
    {
        // Arrange
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("Rate limit exceeded")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var circuitBreaker = HttpClientTranslationPolicies.GetCircuitBreakerPolicy();

        // Act - Circuit breaker should allow the call through and return 429
        var response = await circuitBreaker.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        callCount.ShouldBe(1,"no breaker yet");
    }

    [Fact]
    public async Task TimeoutPolicy_ShouldNotTimeout_WhenRequestIsQuick()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                // Simulate quick response
                return Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)
                    .ContinueWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("success")
                    }, TaskScheduler.Default);
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://test.com")
        };

        var timeoutPolicy = HttpClientTranslationPolicies.GetTimeoutPolicy();

        // Act
        var response = await timeoutPolicy.ExecuteAsync(
            () => httpClient.GetAsync("/test"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}