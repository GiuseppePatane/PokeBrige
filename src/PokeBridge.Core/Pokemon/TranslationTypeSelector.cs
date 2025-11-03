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

        // Yoda for legendary pokemon
        if (pokemon.IsLegendary)
            return TranslationType.Yoda;

        // Yoda for cave-dwelling pokemon
        if (!string.IsNullOrWhiteSpace(pokemon.Habitat) &&
            pokemon.Habitat.Equals("cave", StringComparison.OrdinalIgnoreCase))
            return TranslationType.Yoda;

        // Shakespeare for all others
        return TranslationType.Shakespeare;
    }
}