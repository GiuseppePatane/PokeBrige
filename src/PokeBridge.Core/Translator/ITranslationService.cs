using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Translator;

/// <summary>
/// Service responsible for managing pokemon translations
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Gets or creates a translation for the given pokemon.
    /// If the translation already exists on the pokemon, returns it.
    /// Otherwise, fetches a new translation from the external API and stores it.
    /// </summary>
    /// <param name="pokemon">The pokemon to translate</param>
    /// <param name="translationType">The type of translation to apply</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The translated description</returns>
    Task<Result<string>> GetOrCreateTranslationAsync(
        PokemonRace pokemon,
        TranslationType translationType,
        CancellationToken ct = default);
}