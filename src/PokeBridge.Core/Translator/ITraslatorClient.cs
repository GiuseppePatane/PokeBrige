using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Translator;

/// <summary>
/// Client for external translation API
/// </summary>
public interface ITranslatorClient
{
    /// <summary>
    /// Translates text using the specified translation type
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="translationType">Type of translation to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<Result<string>> GetTranslationAsync(
        string text,
        TranslationType translationType,
        CancellationToken cancellationToken = default);
}