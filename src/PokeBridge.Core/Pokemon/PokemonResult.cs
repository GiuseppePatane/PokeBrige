namespace PokeBridge.Core.Pokemon;



public class PokemonResult
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Habitat { get; init; } = default!;
    public bool IsLegendary { get; init; }
}