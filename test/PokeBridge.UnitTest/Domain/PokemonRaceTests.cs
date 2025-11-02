using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.UnitTest.Domain;

public class PokemonRaceTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsSuccessResult()
    {
        var result = PokemonRace.Create(1, "Pikachu", "Electric mouse", "Forest", false);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Id);
        Assert.Equal("Pikachu", result.Value.Name);
        Assert.Equal("Electric mouse", result.Value.Description);
        Assert.Equal("Forest", result.Value.Habitat);
        Assert.False(result.Value.IsLegendary);
    }

    [Fact]
    public void Create_WithInvalidId_ReturnsFailureResult()
    {
        var result = PokemonRace.Create(0, "Pikachu", "Electric mouse", "Forest", false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation failed for 'id': Id must be greater than zero", result.Error.Message);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsFailureResult()
    {
        var result = PokemonRace.Create(1, "", "Electric mouse", "Forest", false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation failed for 'name': Name cannot be empty", result.Error.Message);
    }

    [Fact]
    public void AddTranslation_WithValidParameters_AddsTranslationSuccessfully()
    {
        var pokemonRace = PokemonRace.Create(1, "Pikachu", "Electric mouse", "Forest", false).Value;

        var result = pokemonRace.AddTranslation(TranslationType.Shakespeare, "Hark! An electric mouse");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hark! An electric mouse", pokemonRace.Translations[TranslationType.Shakespeare]);
    }

    [Fact]
    public void AddTranslation_WithEmptyDescription_ReturnsFailureResult()
    {
        var pokemonRace = PokemonRace.Create(1, "Pikachu", "Electric mouse", "Forest", false).Value;

        var result = pokemonRace.AddTranslation(TranslationType.Shakespeare, "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation failed for 'translatedDescription': Translated description cannot be empty", result.Error.Message);
    }

    [Fact]
    public void AddTranslation_WithInvalidTranslationType_ReturnsFailureResult()
    {
        var pokemonRace = PokemonRace.Create(1, "Pikachu", "Electric mouse", "Forest", false).Value;

        var result = pokemonRace.AddTranslation((TranslationType)999, "Invalid type");

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation failed for 'type': Invalid translation type: 999", result.Error.Message);
    }
}
