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
       
        if(pokemon==null)
        {
            _logger.LogError("Pokemon cannot be null");
            return Result<string>.Failure(new ValidationError(nameof(pokemon), "Pokemon cannot be null"));
        }

        
        if (pokemon.HasTranslation(translationType))
        {
            _logger.LogDebug(
                "Translation already exists for pokemon {PokemonName} with type {TranslationType}",
                pokemon.Name,
                translationType);

            var existingTranslation = pokemon.Translations[translationType];
            return Result<string>.Success(existingTranslation);
        }
        
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
            
            //fallback to original description
            return Result<string>.Success(pokemon.Description);
        }
        
        var addResult = pokemon.AddTranslation(translationType, translationResult.Value);
        if (addResult.IsFailure)
        {
            _logger.LogError(
                "Failed to add translation to pokemon {PokemonName}: {ErrorMessage}",
                pokemon.Name,
                addResult.Error.Message);

            return Result<string>.Failure(addResult.Error);
        }
        
        var saveResult = await _pokemonRepository.Save(pokemon, ct);
        if (saveResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to persist translation for {PokemonName}: {ErrorMessage}.",
                pokemon.Name,
                saveResult.Error.Message);

           // still return the translation even if persisting failed
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