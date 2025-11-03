using Microsoft.Extensions.Logging;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Translator;

/// <summary>
/// Handles pokemon description translations
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly ITranslatorClient _translatorClient;
    private readonly IPokemonRepository _pokemonRepository;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        ITranslatorClient translatorClient,
        IPokemonRepository pokemonRepository,
        ILogger<TranslationService> logger)
    {
        _translatorClient = translatorClient;
        _pokemonRepository = pokemonRepository;
        _logger = logger;
    }

    public async Task<Result<string>> GetOrCreateTranslationAsync(
        PokemonRace pokemon,
        TranslationType translationType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        // Check if translation already exists
        if (pokemon.HasTranslation(translationType))
        {
            _logger.LogDebug(
                "Translation already exists for pokemon {PokemonName} with type {TranslationType}",
                pokemon.Name,
                translationType);

            var existingTranslation = pokemon.Translations[translationType];
            return Result<string>.Success(existingTranslation);
        }

        // Translation doesn't exist - fetch from external API
        _logger.LogInformation(
            "Fetching new translation for pokemon {PokemonName} with type {TranslationType}",
            pokemon.Name,
            translationType);

        var translationResult = await _translatorClient.GetTranslationAsync(
            pokemon.Description,
            translationType,
            ct);

        if (translationResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to retrieve translation for {PokemonName}: {ErrorMessage}. Returning original description.",
                pokemon.Name,
                translationResult.Error.Message);

            // Fallback to original description
            return Result<string>.Success(pokemon.Description);
        }

        // Add translation to pokemon
        var addResult = pokemon.AddTranslation(translationType, translationResult.Value);
        if (addResult.IsFailure)
        {
            _logger.LogError(
                "Failed to add translation to pokemon {PokemonName}: {ErrorMessage}",
                pokemon.Name,
                addResult.Error.Message);

            return Result<string>.Failure(addResult.Error);
        }

        // Persist the translation
        var saveResult = await _pokemonRepository.Save(pokemon, ct);
        if (saveResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to persist translation for {PokemonName}: {ErrorMessage}. Translation will be used but not cached.",
                pokemon.Name,
                saveResult.Error.Message);

            // Don't fail the request if persistence fails - we still have the translation
        }
        else
        {
            _logger.LogInformation(
                "Successfully persisted translation for pokemon {PokemonName} with type {TranslationType}",
                pokemon.Name,
                translationType);
        }

        return Result<string>.Success(translationResult.Value);
    }
}