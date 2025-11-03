using FluentAssertions;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.UnitTest.Core;

public class TranslationTypeSelectorTests
{
    private readonly TranslationTypeSelector _sut;

    public TranslationTypeSelectorTests()
    {
        _sut = new TranslationTypeSelector();
    }

    [Fact]
    public void SelectTranslationType_WhenPokemonIsLegendary_ReturnsYoda()
    {
        // Arrange
        var pokemon = PokemonRace.Create(150, "Mewtwo", "A legendary pokemon", "Rare", true).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Yoda);
    }

    [Fact]
    public void SelectTranslationType_WhenPokemonLivesInCave_ReturnsYoda()
    {
        // Arrange
        var pokemon = PokemonRace.Create(41, "Zubat", "A cave dwelling bat", "cave", false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Yoda);
    }

    [Fact]
    public void SelectTranslationType_WhenHabitatIsCaveWithDifferentCasing_ReturnsYoda()
    {
        // Arrange
        var pokemon = PokemonRace.Create(42, "Golbat", "An evolved cave bat", "CAVE", false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Yoda);
    }

    [Fact]
    public void SelectTranslationType_WhenPokemonIsNormalNonCaveDweller_ReturnsShakespeare()
    {
        // Arrange
        var pokemon = PokemonRace.Create(25, "Pikachu", "An electric mouse", "forest", false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Shakespeare);
    }

    [Fact]
    public void SelectTranslationType_WhenHabitatIsEmpty_ReturnsShakespeare()
    {
        // Arrange
        var pokemon = PokemonRace.Create(1, "Bulbasaur", "A grass type", "", false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Shakespeare);
    }

    [Fact]
    public void SelectTranslationType_WhenHabitatIsNull_ReturnsShakespeare()
    {
        // Arrange
        var pokemon = PokemonRace.Create(1, "Bulbasaur", "A grass type", null, false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Shakespeare);
    }

    [Fact]
    public void SelectTranslationType_WhenPokemonIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _sut.SelectTranslationType(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("mountain")]
    [InlineData("sea")]
    [InlineData("forest")]
    [InlineData("urban")]
    public void SelectTranslationType_WithVariousNonCaveHabitats_ReturnsShakespeare(string habitat)
    {
        // Arrange
        var pokemon = PokemonRace.Create(100, "TestPokemon", "Test description", habitat, false).Value;

        // Act
        var result = _sut.SelectTranslationType(pokemon);

        // Assert
        result.Should().Be(TranslationType.Shakespeare);
    }
}