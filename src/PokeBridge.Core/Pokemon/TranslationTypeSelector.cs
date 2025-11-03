using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

/// <summary>
/// Default implementation of translation type selection logic
/// Rules:
/// - Legendary pokemon -> Yoda
/// - Cave habitat pokemon -> Yoda
/// - All others -> Shakespeare
/// </summary>
public class TranslationTypeSelector : ITranslationTypeSelector
{
    public TranslationType SelectTranslationType(PokemonRace pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        return
            pokemon.IsLegendary
            || !string.IsNullOrWhiteSpace(pokemon.Habitat)
                && pokemon.Habitat.Equals("cave", StringComparison.OrdinalIgnoreCase)
            ? TranslationType.Yoda
            : TranslationType.Shakespeare;
    }
}
