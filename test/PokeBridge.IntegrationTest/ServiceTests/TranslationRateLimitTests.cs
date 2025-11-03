using Microsoft.Extensions.DependencyInjection;
using Moq;
using PokeBridge.Api;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.Translator;
using PokeBridge.IntegrationTest.Infrastructure;
using Shouldly;
using Xunit.Abstractions;

namespace PokeBridge.IntegrationTest.ServiceTests;

/// <summary>
/// Integration tests to verify rate limit handling for translation API
/// </summary>
public class TranslationRateLimitTests : IntegrationTestBase
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

        // Register infrastructure services
        services.AddScoped<IPokemonRepository>(sp =>
        {
            var efRepo = ActivatorUtilities.CreateInstance<PokeBridge.Infrastructure.Pokemon.PokemonEfRepository>(sp);
            var cache = sp.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PokeBridge.Infrastructure.Pokemon.CachedPokemonRepository>>();
            return new PokeBridge.Infrastructure.Pokemon.CachedPokemonRepository(efRepo, cache, logger);
        });

        services.AddScoped<ITranslationTypeSelector, TranslationTypeSelector>();
        services.AddScoped<ITranslationService, TranslationService>();
        services.AddScoped<PokemonService>();
    }

    protected override Task SeedDataAsync()
    {
        _pokemonService = ServiceProvider.GetRequiredService<PokemonService>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetTranslatedPokemon_WhenRateLimitExceeded_ReturnsOriginalDescription()
    {
        // Arrange
        await ClearDatabaseAsync();
        await ClearCacheAsync();

        var pokemon = PokemonRace.Create(
            25,
            "Pikachu",
            "Electric mouse pokemon",
            "forest",
            false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        // Simulate rate limit exceeded
        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(
                It.IsAny<string>(),
                It.IsAny<TranslationType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                new RateLimitExceededError("Rate limit exceeded")));

        // Act
        var result = await _pokemonService.GetTranslatedPokemonRace("Pikachu");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Pikachu");

        // Should return original description when rate limit is hit
        result.Value.Description.ShouldBe("Electric mouse pokemon");

        // Verify translation was attempted
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync(
                "Electric mouse pokemon",
                TranslationType.Shakespeare,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemon_AfterRateLimit_SubsequentRequestsUseCachedOriginal()
    {
        // Arrange
        await ClearDatabaseAsync();
        await ClearCacheAsync();

        var pokemon = PokemonRace.Create(
            150,
            "Mewtwo",
            "Legendary psychic pokemon",
            "rare",
            true).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Mewtwo"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        // First call: rate limit exceeded
        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(
                It.IsAny<string>(),
                TranslationType.Yoda,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                new RateLimitExceededError("Rate limit exceeded")));

        // Act - First request (rate limit hit)
        var result1 = await _pokemonService.GetTranslatedPokemonRace("Mewtwo");

        // Act - Second request (should use database, no translation call)
        var result2 = await _pokemonService.GetTranslatedPokemonRace("Mewtwo");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.Description.ShouldBe("Legendary psychic pokemon"); // Original

        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Description.ShouldBe("Legendary psychic pokemon"); // Still original

        // Translation API should only be called once (first request)
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync(
                It.IsAny<string>(),
                It.IsAny<TranslationType>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Pokemon API should also only be called once (cached in DB)
        _pokemonClientMock.Verify(
            x => x.GetPokemonRaceAsync("Mewtwo"),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemon_WhenRateLimitThenSuccess_UpdatesTranslation()
    {
        // Arrange
        await ClearDatabaseAsync();
        await ClearCacheAsync();

        var pokemon = PokemonRace.Create(
            6,
            "Charizard",
            "Fire dragon pokemon",
            "mountain",
            false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Charizard"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        // First call: rate limit exceeded
        _translatorClientMock
            .SetupSequence(x => x.GetTranslationAsync(
                "Fire dragon pokemon",
                TranslationType.Shakespeare,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                new RateLimitExceededError("Rate limit exceeded")))
            .ReturnsAsync(Result<string>.Success(
                "Hark! A fire dragon most fierce"));

        // Act - First request (rate limit)
        var result1 = await _pokemonService.GetTranslatedPokemonRace("Charizard");

        // Clear cache to force new translation attempt
        await ClearCacheAsync();

        // Act - Second request (translation succeeds)
        var result2 = await _pokemonService.GetTranslatedPokemonRace("Charizard");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.Description.ShouldBe("Fire dragon pokemon"); // Original

        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Description.ShouldBe("Hark! A fire dragon most fierce"); // Translated

        // Translation should have been attempted twice
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync(
                "Fire dragon pokemon",
                TranslationType.Shakespeare,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetTranslatedPokemon_MultiplePokemons_RateLimitAffectsOnlyUntranslated()
    {
        // Arrange
        await ClearDatabaseAsync();
        await ClearCacheAsync();

        var pikachu = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        var bulbasaur = PokemonRace.Create(1, "Bulbasaur", "Grass type", "grassland", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pikachu));

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Bulbasaur"))
            .ReturnsAsync(Result<PokemonRace>.Success(bulbasaur));

        // Pikachu translation succeeds
        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(
                "Electric mouse",
                TranslationType.Shakespeare,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Hark! An electric mouse"));

        // Bulbasaur hits rate limit
        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(
                "Grass type",
                TranslationType.Shakespeare,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                new RateLimitExceededError("Rate limit exceeded")));

        // Act
        var pikachuResult = await _pokemonService.GetTranslatedPokemonRace("Pikachu");
        var bulbasaurResult = await _pokemonService.GetTranslatedPokemonRace("Bulbasaur");

        // Assert
        pikachuResult.IsSuccess.ShouldBeTrue();
        pikachuResult.Value.Description.ShouldBe("Hark! An electric mouse"); // Translated

        bulbasaurResult.IsSuccess.ShouldBeTrue();
        bulbasaurResult.Value.Description.ShouldBe("Grass type"); // Original (rate limit)
    }
}