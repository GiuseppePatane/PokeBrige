using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeBridge.Core.Pokemon;
using PokeBridge.Infrastructure.Pokemon;
using PokeBridge.IntegrationTest.Infrastructure;

namespace PokeBridge.IntegrationTest.RepositoryTests;

/// <summary>
/// Integration tests for CachedPokemonRepository with real database and cache
/// </summary>
public class CachedPokemonRepositoryIntegrationTests : IntegrationTestBase
{
    private IPokemonRepository _repository = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Register EF repository
        services.AddScoped<PokemonEfRepository>();

        // Register cached repository (decorator pattern)
        services.AddScoped<IPokemonRepository>(sp =>
        {
            var innerRepository = sp.GetRequiredService<PokemonEfRepository>();
            var cache = sp.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>();
            var logger = sp.GetRequiredService<ILogger<CachedPokemonRepository>>();
            return new CachedPokemonRepository(innerRepository, cache, logger);
        });
    }

    protected override async Task SeedDataAsync()
    {
        _repository = ServiceProvider.GetRequiredService<IPokemonRepository>();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetByName_WhenPokemonExistsInDatabase_ReturnsPokemonAndCachesIt()
    {
        // Arrange
        var pokemon = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        await _repository.Save(pokemon);
        await ClearCacheAsync(); // Ensure cache is empty

        // Act - First call should hit database
        var result1 = await _repository.GetByName("Pikachu");

        // Clear database but keep cache
        await ClearDatabaseAsync();

        // Act - Second call should hit cache
        var result2 = await _repository.GetByName("Pikachu");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.Name.ShouldBe("Pikachu");

        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Name.ShouldBe("Pikachu");
        result2.Value.Id.ShouldBe(25);
    }

    [Fact]
    public async Task GetByName_WithCaseInsensitiveName_ReturnsCorrectPokemon()
    {
        // Arrange
        var pokemon = PokemonRace.Create(1, "Bulbasaur", "Grass type", "grassland", false).Value;
        await _repository.Save(pokemon);
        await ClearCacheAsync();

        // Act
        var resultLower = await _repository.GetByName("bulbasaur");
        var resultUpper = await _repository.GetByName("BULBASAUR");
        var resultMixed = await _repository.GetByName("BuLbAsAuR");

        // Assert
        resultLower.IsSuccess.ShouldBeTrue();
        resultUpper.IsSuccess.ShouldBeTrue();
        resultMixed.IsSuccess.ShouldBeTrue();

        resultLower.Value.Name.ShouldBe("Bulbasaur");
        resultUpper.Value.Name.ShouldBe("Bulbasaur");
        resultMixed.Value.Name.ShouldBe("Bulbasaur");
    }

    [Fact]
    public async Task Save_WhenPokemonIsSaved_InvalidatesCache()
    {
        // Arrange
        var pokemon = PokemonRace.Create(150, "Mewtwo", "Legendary pokemon", "rare", true).Value;
        await _repository.Save(pokemon);
      
        // Fetch to populate cache
        await _repository.GetByName("Mewtwo");

        // Act - Update pokemon with translation
        pokemon.AddTranslation(Core.Shared.TranslationType.Yoda, "Legendary, this pokemon is");
        await _repository.Save(pokemon);

        // Assert - Should get updated version from database (cache invalidated)
        var result = await _repository.GetByName("Mewtwo");
        result.IsSuccess.ShouldBeTrue();
        result.Value.HasTranslation(Core.Shared.TranslationType.Yoda).ShouldBeTrue();
    }

    [Fact]
    public async Task GetByName_WhenPokemonNotFound_ReturnsFailure()
    {
        // Act
        var result = await _repository.GetByName("NonExistentPokemon");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("No pokemon found with name 'NonExistentPokemon'");
    }

    [Fact]
    public async Task Save_NewPokemon_PersistsToDatabase()
    {
        // Arrange
        var pokemon = PokemonRace.Create(6, "Charizard", "Fire dragon", "mountain", false).Value;

        // Act
        var saveResult = await _repository.Save(pokemon);

        // Assert
        saveResult.IsSuccess.ShouldBeTrue();

        // Verify persistence by clearing cache and fetching
        await ClearCacheAsync();
        var fetchResult = await _repository.GetByName("Charizard");
        fetchResult.IsSuccess.ShouldBeTrue();
        fetchResult.Value.Id.ShouldBe(6);
        fetchResult.Value.Description.ShouldBe("Fire dragon");
    }

    [Fact]
    public async Task Save_UpdateExistingPokemon_UpdatesInDatabase()
    {
        // Arrange
        var pokemon = PokemonRace.Create(7, "Squirtle", "Water turtle", "water", false).Value;
        await _repository.Save(pokemon);

        // Act - Add translation and save
        pokemon.AddTranslation(Core.Shared.TranslationType.Shakespeare, "Hark! A water turtle");
        var updateResult = await _repository.Save(pokemon);

        // Assert
        updateResult.IsSuccess.ShouldBeTrue();
        
        var fetchResult = await _repository.GetByName("Squirtle");
        fetchResult.IsSuccess.ShouldBeTrue();
        fetchResult.Value.HasTranslation(Core.Shared.TranslationType.Shakespeare).ShouldBeTrue();
    }

    [Fact]
    public async Task CacheFailSafe_WhenDatabaseIsDown_ReturnsCachedData()
    {
        // Arrange
        var pokemon = PokemonRace.Create(143, "Snorlax", "Sleeping giant", "mountain", false).Value;
        await _repository.Save(pokemon);

        // Populate cache
        var firstResult = await _repository.GetByName("Snorlax");
        firstResult.IsSuccess.ShouldBeTrue();

        // Simulate database being down by disposing DbContext
        await DbContext.DisposeAsync();

        // Act - Should return cached data even though DB is down
        var cachedResult = await _repository.GetByName("Snorlax");

        // Assert
        cachedResult.IsSuccess.ShouldBeTrue();
        cachedResult.Value.Name.ShouldBe("Snorlax");
    }

    [Fact]
    public async Task GetByName_WithMultiplePokemon_EachHasOwnCacheEntry()
    {
        // Arrange
        var pikachu = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        var raichu = PokemonRace.Create(26, "Raichu", "Electric evolved", "forest", false).Value;

        await _repository.Save(pikachu);
        await _repository.Save(raichu);

        await ClearCacheAsync();

        // Act - Fetch both to populate cache
        var pikachuResult1 = await _repository.GetByName("Pikachu");
        var raichuResult1 = await _repository.GetByName("Raichu");

        // Clear database
        await ClearDatabaseAsync();

        // Fetch from cache
        var pikachuResult2 = await _repository.GetByName("Pikachu");
        var raichuResult2 = await _repository.GetByName("Raichu");

        // Assert - Both should be retrieved from cache
        pikachuResult1.IsSuccess.ShouldBeTrue();
        raichuResult1.IsSuccess.ShouldBeTrue();
        pikachuResult2.IsSuccess.ShouldBeTrue();
        raichuResult2.IsSuccess.ShouldBeTrue();

        pikachuResult2.Value.Id.ShouldBe(25);
        raichuResult2.Value.Id.ShouldBe(26);
    }
}