using System.Net;
using PokeApiNet;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.Infrastructure.Pokemon;

public class PokeClient : IPokemonClient
{
    private readonly PokeApiClient _pokeApiClient;

    public PokeClient(PokeApiClient pokeApiClient)
    {
        _pokeApiClient = pokeApiClient;
    }

    /// <summary>
    /// Get Pokemon Race Information using PokeApiClient
    /// </summary>
    /// <param name="pokemon"></param>
    /// <returns></returns>
    public async Task<Result<PokemonRace>> GetPokemonRaceAsync(string pokemon)
    {
        try
        {
            var pokemonSpecies = await _pokeApiClient.GetResourceAsync<PokemonSpecies>(
                pokemon.ToLower().Trim()
            );
            return pokemonSpecies?.MapToPokemonRace()!;
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return Result<PokemonRace>.Failure(new NotFoundError(pokemon));
        }
        catch (Exception e)
        {
            return Result<PokemonRace>.Failure(new ApiError(e.Message));
        }
    }
}

public record ApiError : DomainError
{
    public ApiError(string message)
        : base("POKEMON_API_ERROR", message) { }
}
