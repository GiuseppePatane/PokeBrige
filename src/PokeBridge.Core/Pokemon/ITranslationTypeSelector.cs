using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

/// <summary>
/// Selects the appropriate translation type based on pokemon characteristics
/// </summary>
public interface ITranslationTypeSelector
{
    /// <summary>
    /// Determines which translation type should be used for the given pokemon
    /// </summary>
    /// <param name="pokemon">The pokemon to evaluate</param>
    /// <returns>The appropriate translation type</returns>
    TranslationType SelectTranslationType(PokemonRace pokemon);
}