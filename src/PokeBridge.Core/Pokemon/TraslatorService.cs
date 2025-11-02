using Microsoft.Extensions.Logging;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;

namespace PokeBridge.Core.Pokemon;

public class PokemonService
{
    private readonly IPokemonClient _pokemonClient;
    private readonly ITranslatorClient _translatorClient;
    private readonly ILogger<PokemonService> _logger;

    public PokemonService(
        IPokemonClient pokemonClient,
        ITranslatorClient translatorClient,
        ILogger<PokemonService> logger
    )
    {
        _pokemonClient = pokemonClient;
        _translatorClient = translatorClient;
        _logger = logger;
    }

    public async Task<Result<PokemonResult>> GetTranslatedPokemonRace(string pokemonName)
    {
        _logger.LogInformation("Request translated description for pokemon: {PokemonName}", pokemonName);
        var pokemonRace = await GetPokemonRace(pokemonName);
        if (pokemonRace.IsFailure)
        {
            _logger.LogInformation("Failed to retrieve pokemon info: {ErrorMessage}", pokemonRace.Error.Message);
            return Result<PokemonResult>.Failure(pokemonRace.Error);
        }

        var description = pokemonRace.Value.Description;
        TranslationType translationType =
            pokemonRace.Value.IsLegendary || pokemonRace.Value.Habitat.ToLower() == "cave"
                ? TranslationType.Yoda
                : TranslationType.Shakespeare;
        _logger.LogInformation("Selected translation type: {TranslationType}", translationType);
        var translationResult = await _translatorClient.GetTranslationAsync(
            description,
            translationType
        );
        if (translationResult.IsFailure)
        {
            _logger.LogInformation("Failed to retrieve translation: {ErrorMessage}", translationResult.Error.Message);
        }
        var result = pokemonRace.Value.ToPokemonResult(translationType);
        return Result<PokemonResult>.Success(result);
    }

    public async Task<Result<PokemonRace>> GetPokemonRace(string pokemonName)
    {
        var pokemonRaceResult = await _pokemonClient.GetPokemonRaceAsync(pokemonName);
        return pokemonRaceResult;
    }
}
