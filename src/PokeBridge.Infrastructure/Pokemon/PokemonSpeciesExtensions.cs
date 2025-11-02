using System.Text.RegularExpressions;
using PokeApiNet;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.Infrastructure.Pokemon;

public static partial class PokemonSpeciesExtensions
{
    public static Result<PokemonRace> MapToPokemonRace(
        this PokemonSpecies? pokemonSpecies,
        string language = "en"
    )
    {
        if (pokemonSpecies is null || pokemonSpecies.Id <= 0)
            return Result<PokemonRace>.Failure(new PokemonNotFoundError("null"));

        var description = ParseFlavorTextEntries(
            pokemonSpecies.FlavorTextEntries,
            pokemonSpecies.Name,
            language
        );

        return PokemonRace.Create(
            pokemonSpecies.Id,
            pokemonSpecies.Name,
            description,
            pokemonSpecies.Habitat?.Name ?? string.Empty,
            pokemonSpecies.IsLegendary
        );
    }
    
    private static string ParseFlavorTextEntries(
        List<PokemonSpeciesFlavorTexts>? flavorTextsList,
        string? name,
        string language
    )
    {
        if (flavorTextsList is null || string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var entries = flavorTextsList
            .Where(f => f.Language.Name.Equals(language, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.FlavorText.RemoveInvalidCharacters())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var match =
            entries.FirstOrDefault(t => t!.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? entries.FirstOrDefault(t => t!.Contains(name, StringComparison.OrdinalIgnoreCase))
            ?? entries.FirstOrDefault()
            ?? string.Empty;

        return match;
    }

    /// <summary>
    /// Remove control characters like tab & new lines from text.
    /// </summary>
    private static string? RemoveInvalidCharacters(this string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : RemoveInvalidCharactersRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"\t|\n|\r|\f")]
    private static partial Regex RemoveInvalidCharactersRegex();
}
