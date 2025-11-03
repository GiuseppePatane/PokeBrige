using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.Translator.Model;

namespace PokeBridge.Infrastructure.Translator;

public class TranslatorHttpClient : ITranslatorClient
{
    private readonly HttpClient _client;
    private readonly ILogger<TranslatorHttpClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public TranslatorHttpClient(HttpClient client, ILogger<TranslatorHttpClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<string>> GetTranslationAsync(
        string text,
        TranslationType translationType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Requesting {TranslationType} translation for text: {Text}",
                translationType,
                text);

            Uri url;
            switch (translationType)
            {
                case TranslationType.Shakespeare:
                    url = TranslationUrlBuilder.BuildShakespeareTranslationUri(text);
                    break;
                case TranslationType.Yoda:
                    url = TranslationUrlBuilder.BuildYodaTranslationUri(text);
                    break;
                default:
                    return Result<string>.Failure(
                        new TranslatorClientError("Unsupported translation type"));
            }

            var response = await _client.GetAsync(url, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ErrorResponse>(content, JsonOptions);
                _logger.LogError("Translation API error: {ErrorMessage}", error?.Error?.Message);
                return Result<string>.Failure(
                    new TranslatorClientError(error?.Error?.Message ?? "Translation API error")
                );
            }

            var translationResponse = JsonSerializer.Deserialize<TranslationResponse>(
                content,
                JsonOptions
            );

            var translated = translationResponse?.Contents?.Translated;
            _logger.LogInformation("Received translation: {TranslatedText}", translated);

            return string.IsNullOrWhiteSpace(translated)
                ? Result<string>.Failure(new TranslatorClientError("Empty translation received"))
                : Result<string>.Success(translated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while getting translation");
            return Result<string>.Failure(new TranslatorClientError(ex.Message));
        }
    }


}

public record TranslatorClientError(string Message)
    : DomainError("TRANSLATOR_CLIENT_ERROR", Message);
