using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

public interface IPokemonRepository
{
    /// <summary>
    ///  Get the pokemon race by name
    /// </summary>
    /// <param name="name"> the name of the pokemon</param>
    /// <param name="ct"> cancellation token</param>
    /// <returns> The pokemon if found</returns>
    Task<Result<PokemonRace>> GetByName(string name, CancellationToken ct = default);
    
    /// <summary>
    ///  Upsert the given pokemon race
    /// </summary>
    /// <param name="pokemon"> the pokemon to upsert</param>
    /// <param name="ct"> cancellation token</param>
    /// <returns> The upserted pokemon</returns>
    Task<Result<PokemonRace>> Save(PokemonRace pokemon, CancellationToken ct = default);
}