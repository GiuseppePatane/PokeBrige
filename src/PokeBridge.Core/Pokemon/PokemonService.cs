using Microsoft.Extensions.Logging;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;

namespace PokeBridge.Core.Pokemon;

/// <summary>
/// Main service for pokemon-related operations
/// Orchestrates between pokemon retrieval, translation, and persistence
/// </summary>
public class PokemonService
{
    private readonly IPokemonClient _pokemonClient;
    private readonly IPokemonRepository _pokemonRepository;
    private readonly ITranslationService _translationService;
    private readonly ITranslationTypeSelector _translationTypeSelector;
    private readonly ILogger<PokemonService> _logger;

    public PokemonService(
        IPokemonClient pokemonClient,
        IPokemonRepository pokemonRepository,
        ITranslationService translationService,
        ITranslationTypeSelector translationTypeSelector,
        ILogger<PokemonService> logger)
    {
        _pokemonClient = pokemonClient;
        _pokemonRepository = pokemonRepository;
        _translationService = translationService;
        _translationTypeSelector = translationTypeSelector;
        _logger = logger;
    }

    /// <summary>
    /// Gets basic pokemon information without translation
    /// </summary>
    /// <param name="pokemonName">The name of the pokemon</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pokemon result with original description</returns>
    public async Task<Result<PokemonResult>> GetPokemon(
        string pokemonName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting pokemon information for {PokemonName}", pokemonName);

        if (string.IsNullOrWhiteSpace(pokemonName))
        {
            return Result<PokemonResult>.Failure(
                new ValidationError(nameof(pokemonName), "Pokemon name cannot be empty"));
        }

        var pokemonRace = await GetPokemonRaceAsync(pokemonName, ct);

        if (pokemonRace.IsFailure)
        {
            _logger.LogWarning(
                "Failed to retrieve pokemon {PokemonName}: {ErrorMessage}",
                pokemonName,
                pokemonRace.Error.Message);

            return Result<PokemonResult>.Failure(pokemonRace.Error);
        }

        var result = pokemonRace.Value.ToPokemonResult(TranslationType.None);
        return Result<PokemonResult>.Success(result);
    }

    /// <summary>
    /// Gets pokemon information with translated description
    /// Translation type is automatically selected based on pokemon characteristics
    /// </summary>
    /// <param name="pokemonName">The name of the pokemon</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pokemon result with translated description</returns>
    public async Task<Result<PokemonResult>> GetTranslatedPokemonRace(
        string pokemonName,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Getting translated pokemon information for {PokemonName}",
            pokemonName);

        if (string.IsNullOrWhiteSpace(pokemonName))
        {
            return Result<PokemonResult>.Failure(
                new ValidationError(nameof(pokemonName), "Pokemon name cannot be empty"));
        }

        // Get pokemon race
        var pokemonRace = await GetPokemonRaceAsync(pokemonName, ct);
        if (pokemonRace.IsFailure)
        {
            _logger.LogWarning(
                "Failed to retrieve pokemon {PokemonName}: {ErrorMessage}",
                pokemonName,
                pokemonRace.Error.Message);

            return Result<PokemonResult>.Failure(pokemonRace.Error);
        }

        // Determine translation type
        var translationType = _translationTypeSelector.SelectTranslationType(pokemonRace.Value);

        _logger.LogInformation(
            "Selected translation type {TranslationType} for pokemon {PokemonName}",
            translationType,
            pokemonName);

        // Get or create translation
        var translationResult = await _translationService.GetOrCreateTranslationAsync(
            pokemonRace.Value,
            translationType,
            ct);

        if (translationResult.IsFailure)
        {
            _logger.LogError(
                "Failed to get translation for {PokemonName}: {ErrorMessage}",
                pokemonName,
                translationResult.Error.Message);

            return Result<PokemonResult>.Failure(translationResult.Error);
        }

        // Build result with translated description
        var result = new PokemonResult
        {
            Name = pokemonRace.Value.Name,
            Description = translationResult.Value,
            Habitat = pokemonRace.Value.Habitat,
            IsLegendary = pokemonRace.Value.IsLegendary
        };

        return Result<PokemonResult>.Success(result);
    }

    /// <summary>
    /// Gets pokemon race from repository or external API
    /// Handles caching and persistence automatically
    /// </summary>
    /// <param name="pokemonName">The name of the pokemon</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pokemon race entity</returns>
    private async Task<Result<PokemonRace>> GetPokemonRaceAsync(
        string pokemonName,
        CancellationToken ct = default)
    {
        
        var pokemonRaceResult = await _pokemonRepository.GetByName(pokemonName, ct);

        if (pokemonRaceResult.IsSuccess)
        {
            _logger.LogDebug(
                "Pokemon {PokemonName} found in repository",
                pokemonName);

            return pokemonRaceResult;
        }
        
        _logger.LogInformation(
            "Pokemon {PokemonName} not found in repository, fetching from external API",
            pokemonName);

        pokemonRaceResult = await _pokemonClient.GetPokemonRaceAsync(pokemonName);

        if (pokemonRaceResult.IsFailure)
        {
            return pokemonRaceResult;
        }
        
        var saveResult = await _pokemonRepository.Save(pokemonRaceResult.Value, ct);

        if (saveResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to save pokemon {PokemonName} to repository: {ErrorMessage}. Continuing with in-memory data.",
                pokemonName,
                saveResult.Error.Message);
            
        }
        else
        {
            _logger.LogInformation(
                "Successfully saved pokemon {PokemonName} to repository",
                pokemonName);
        }

        return pokemonRaceResult;
    }
}