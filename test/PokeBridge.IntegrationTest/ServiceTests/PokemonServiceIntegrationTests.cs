using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.Pokemon;
using PokeBridge.IntegrationTest.Infrastructure;

namespace PokeBridge.IntegrationTest.ServiceTests;


public class PokemonServiceIntegrationTests : IntegrationTestBase
{
    private PokemonService _pokemonService = null!;
    private Mock<IPokemonClient> _pokemonClientMock = null!;
    private Mock<ITranslatorClient> _translatorClientMock = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Mock external API clients
        _pokemonClientMock = new Mock<IPokemonClient>();
        _translatorClientMock = new Mock<ITranslatorClient>();

        services.AddSingleton(_pokemonClientMock.Object);
        services.AddSingleton(_translatorClientMock.Object);
        
        services.AddScoped<PokemonEfRepository>();
        services.AddScoped<IPokemonRepository>(sp =>
        {
            var innerRepository = sp.GetRequiredService<PokemonEfRepository>();
            var cache = sp.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>();
            var logger = sp.GetRequiredService<ILogger<CachedPokemonRepository>>();
            return new CachedPokemonRepository(innerRepository, cache, logger);
        });
        
        services.AddScoped<ITranslationTypeSelector, TranslationTypeSelector>();
        services.AddScoped<ITranslationService, TranslationService>();
        services.AddScoped<PokemonService>();
    }

    protected override Task SeedDataAsync()
    {
        _pokemonService = ServiceProvider.GetRequiredService<PokemonService>();
        // per il momento skippiamo
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetPokemon_WhenPokemonNotInCache_FetchesFromExternalAPIAndCaches()
    {
        // Arrange
        var expectedPokemon = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(expectedPokemon));

        // Act - First call
        var result1 = await _pokemonService.GetPokemon("Pikachu");

        // Act - Second call (should use cache/database, not external API)
        var result2 = await _pokemonService.GetPokemon("Pikachu");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.Name.ShouldBe("Pikachu");
        result1.Value.Description.ShouldBe("Electric mouse");

        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Name.ShouldBe("Pikachu");

        // Verify external API was called only once
        _pokemonClientMock.Verify(
            x => x.GetPokemonRaceAsync("Pikachu"),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemonRace_ForLegendaryPokemon_UsesYodaTranslation()
    {
        // Arrange
        var legendaryPokemon = PokemonRace.Create(150, "Mewtwo", "Legendary psychic pokemon", "rare", true).Value;
        var yodaTranslation = "Legendary psychic pokemon, this is";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Mewtwo"))
            .ReturnsAsync(Result<PokemonRace>.Success(legendaryPokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Legendary psychic pokemon", TranslationType.Yoda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(yodaTranslation));

        // Act
        var result = await _pokemonService.GetTranslatedPokemonRace("Mewtwo");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe(yodaTranslation);
        result.Value.IsLegendary.ShouldBeTrue();

        // Should use Yoda translation
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync("Legendary psychic pokemon", TranslationType.Yoda, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemonRace_ForCavePokemon_UsesYodaTranslation()
    {
        // Arrange
        var cavePokemon = PokemonRace.Create(41, "Zubat", "Cave dwelling bat", "cave", false).Value;
        var yodaTranslation = "Cave dwelling bat, it is";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Zubat"))
            .ReturnsAsync(Result<PokemonRace>.Success(cavePokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Cave dwelling bat", TranslationType.Yoda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(yodaTranslation));

        // Act
        var result = await _pokemonService.GetTranslatedPokemonRace("Zubat");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe(yodaTranslation);

        // Should use Yoda translation
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync("Cave dwelling bat", TranslationType.Yoda, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemonRace_ForNormalPokemon_UsesShakespeareTranslation()
    {
        // Arrange
        var normalPokemon = PokemonRace.Create(1, "Bulbasaur", "Grass type pokemon", "grassland", false).Value;
        var shakespeareTranslation = "Hark! A grass type pokemon";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Bulbasaur"))
            .ReturnsAsync(Result<PokemonRace>.Success(normalPokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Grass type pokemon", TranslationType.Shakespeare, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(shakespeareTranslation));

        // Act
        var result = await _pokemonService.GetTranslatedPokemonRace("Bulbasaur");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe(shakespeareTranslation);

        // no legendary or cave, should use Shakespeare translation
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync("Grass type pokemon", TranslationType.Shakespeare, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemonRace_WhenTranslationCached_DoesNotCallExternalAPI()
    {
        // Arrange
        var pokemon = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        var translation = "Hark! An electric mouse";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Electric mouse", TranslationType.Shakespeare, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(translation));

        // fetch and cache translation
        var result1 = await _pokemonService.GetTranslatedPokemonRace("Pikachu");

        // should skip external api call. Cache should be used.
        var result2 = await _pokemonService.GetTranslatedPokemonRace("Pikachu");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Description.ShouldBe(translation);

        // translation api should be called only once
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync(It.IsAny<string>(), It.IsAny<TranslationType>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemonRace_WhenTranslationFails_ReturnsOriginalDescription()
    {
        // Arrange
        var pokemon = PokemonRace.Create(7, "Squirtle", "Water turtle", "water", false).Value;
        var translationError = new PersistenceError("Translation service unavailable");

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Squirtle"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(It.IsAny<string>(), It.IsAny<TranslationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(translationError));

        // Act
        var result = await _pokemonService.GetTranslatedPokemonRace("Squirtle");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe("Water turtle"); 
    }

    [Fact]
    public async Task GetPokemon_WithEmptyName_ReturnsValidationError()
    {
        // Act
        var result = await _pokemonService.GetPokemon("");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public async Task GetPokemon_WhenExternalAPIFails_ReturnsError()
    {
        // Arrange
        var error = new NotFoundError("Pokemon not found");

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("NonExistent"))
            .ReturnsAsync(Result<PokemonRace>.Failure(error));

        // Act
        var result = await _pokemonService.GetPokemon("NonExistent");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("POKEMON_RACE_NOT_FOUND");
    }

    [Fact]
    public async Task GetPokemon_ConcurrentRequests_HandlesCorrectly()
    {
        // Arrange
        var pikachu = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        var charizard = PokemonRace.Create(6, "Charizard", "Fire dragon", "mountain", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pikachu));

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Charizard"))
            .ReturnsAsync(Result<PokemonRace>.Success(charizard));

        // Act - Concurrent requests
        var tasks = new[]
        {
            _pokemonService.GetPokemon("Pikachu"),
            _pokemonService.GetPokemon("Charizard"),
            _pokemonService.GetPokemon("Pikachu"),
            _pokemonService.GetPokemon("Charizard")
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.ShouldAllBe(r => r.IsSuccess);
        results.Count(r => r.Value.Name == "Pikachu").ShouldBe(2);
        results.Count(r => r.Value.Name == "Charizard").ShouldBe(2);
    }
}