using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

public interface IPokemonRepository
{
    Task<Result<PokemonRace>> GetByName(string name, CancellationToken ct = default);
    Task<Result<PokemonRace>> Save(PokemonRace pokemon, CancellationToken ct = default);
}