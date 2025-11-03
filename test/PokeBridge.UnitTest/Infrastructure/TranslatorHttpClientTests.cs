using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.Translator;
using Shouldly;

namespace PokeBridge.UnitTest.Infrastructure;

public class TranslatorHttpClientTests
{
    private readonly Mock<ILogger<TranslatorHttpClient>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly TranslatorHttpClient _translatorClient;

    public TranslatorHttpClientTests()
    {
        _loggerMock = new Mock<ILogger<TranslatorHttpClient>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.funtranslations.com")
        };
        _translatorClient = new TranslatorHttpClient(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetTranslationAsync_WhenRateLimitExceeded_ReturnsRateLimitError()
    {
        // Arrange
        var responseContent = @"{
            ""error"": {
                ""code"": 429,
                ""message"": ""Too Many Requests: Rate limit of 5 requests per hour exceeded. Public API rate limit reached""
            }
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _translatorClient.GetTranslationAsync(
            "Hello world",
            TranslationType.Shakespeare);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<RateLimitExceededError>();
        result.Error.Code.ShouldBe("RATE_LIMIT_EXCEEDED");
        result.Error.Message.ShouldContain("rate limit exceeded");
    }

    [Fact]
    public async Task GetTranslationAsync_WhenSuccessful_ReturnsTranslation()
    {
        // Arrange
        var responseContent = @"{
            ""success"": {
                ""total"": 1
            },
            ""contents"": {
                ""translated"": ""Valorous morrow, sir"",
                ""text"": ""Good morning"",
                ""translation"": ""shakespeare""
            }
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _translatorClient.GetTranslationAsync(
            "Good morning",
            TranslationType.Shakespeare);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Valorous morrow, sir");
    }

    [Fact]
    public async Task GetTranslationAsync_WhenApiError_ReturnsTranslatorClientError()
    {
        // Arrange
        var responseContent = @"{
            ""error"": {
                ""code"": 400,
                ""message"": ""Bad Request: missing text parameter""
            }
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(responseContent)
            });

        // Act
        var result = await _translatorClient.GetTranslationAsync(
            "",
            TranslationType.Shakespeare);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<TranslatorClientError>();
        result.Error.Message.ShouldContain("Bad Request");
    }

    [Fact]
    public async Task GetTranslationAsync_WhenNetworkError_ReturnsTranslatorClientError()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _translatorClient.GetTranslationAsync(
            "Hello",
            TranslationType.Yoda);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<TranslatorClientError>();
        result.Error.Message.ShouldContain("Network error");
    }
}