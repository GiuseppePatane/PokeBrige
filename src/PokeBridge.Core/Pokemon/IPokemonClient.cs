using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

public interface IPokemonClient
{
    /// <summary>
    ///   Get the pokemon race description of the given pokemon name
    /// </summary>
    /// <param name="pokemonName"></param>
    /// <returns></returns>
    public Task<Result<PokemonRace>> GetPokemonRaceAsync(string pokemonName);
}