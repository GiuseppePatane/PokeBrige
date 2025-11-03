using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using PokeBridge.Core;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.EF;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace PokeBridge.IntegrationTest.ApiTests;

/// <summary>
/// Integration tests for Pokemon API endpoints with real infrastructure with
/// Testcontainers for PostgreSQL and Redis, and mocked external API clients.
/// </summary>
public class PokemonControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private PostgreSqlContainer _postgresContainer = null!;
    private RedisContainer _redisContainer = null!;
    private Mock<IPokemonClient> _pokemonClientMock = null!;
    private Mock<ITranslatorClient> _translatorClientMock = null!;

    public async Task InitializeAsync()
    {
        // Start containers
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("pokebridgetest")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync()
        );

        // Create mocks
        _pokemonClientMock = new Mock<IPokemonClient>();
        _translatorClientMock = new Mock<ITranslatorClient>();

        // Create factory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Override configuration with test values
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:PokeBridgeDatabase"] = _postgresContainer.GetConnectionString(),
                        ["Redis:Configuration"] = _redisContainer.GetConnectionString(),
                        ["Redis:InstanceName"] = "PokeBridgeTest_",
                        ["HttpClients:FunTranslationsApiBaseUrl"] = "https://api.funtranslations.com/translate/"
                    }!);
                });

                builder.ConfigureTestServices(services =>
                {
                    // Reconfigure NpgsqlDataSource with test connection string
                    services.RemoveAll<NpgsqlDataSource>();
                    services.AddSingleton<NpgsqlDataSource>(sp =>
                    {
                        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(_postgresContainer.GetConnectionString());
                        dataSourceBuilder.EnableDynamicJson();
                        return dataSourceBuilder.Build();
                    });
                    
                    services.RemoveAll<DbContextOptions<PokeBridgeDbContext>>();
                    services.RemoveAll<PokeBridgeDbContext>();
                    services.AddDbContext<PokeBridgeDbContext>((sp, options) =>
                    {
                        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
                        options.UseNpgsql(dataSource);
                        options.EnableSensitiveDataLogging();
                        options.ConfigureWarnings(warnings =>
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));
                    });

                    // Exchange real clients with mocks
                    services.RemoveAll<IPokemonClient>();
                    services.AddSingleton(_pokemonClientMock.Object);

                    services.RemoveAll<ITranslatorClient>();
                    services.AddSingleton(_translatorClientMock.Object);
                });
            });

        _client = _factory.CreateClient();

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PokeBridgeDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task GetPokemon_ReturnsOkWithPokemonData()
    {
        // Arrange
        var pokemon = PokemonRace.Create(25, "Pikachu", "Electric mouse pokemon", "forest", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        // Act
        var response = await _client.GetAsync("/Pokemon/Pikachu");
        var result = await response.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Pikachu");
        result.Description.ShouldBe("Electric mouse pokemon");
        result.Habitat.ShouldBe("forest");
        result.IsLegendary.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPokemon_WhenCached_DoesNotCallExternalAPI()
    {
        // Arrange
        var pokemon = PokemonRace.Create(1, "Bulbasaur", "Grass type", "grassland", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Bulbasaur"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        // Act - First call
        var response1 = await _client.GetAsync("/Pokemon/Bulbasaur");
        
        // Act - Second call (should use cached data)
        var response2 = await _client.GetAsync("/Pokemon/Bulbasaur");

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        
        _pokemonClientMock.Verify(
            x => x.GetPokemonRaceAsync("Bulbasaur"),
            Times.Once);
    }

    [Fact]
    public async Task GetTranslatedPokemon_ForLegendaryPokemon_ReturnsYodaTranslation()
    {
        // Arrange
        var pokemon = PokemonRace.Create(150, "Mewtwo", "Legendary psychic pokemon", "rare", true).Value;
        var yodaTranslation = "Legendary psychic pokemon, this is";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Mewtwo"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Legendary psychic pokemon", TranslationType.Yoda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(yodaTranslation));

        // Act
        var response = await _client.GetAsync("/Pokemon/translated/Mewtwo");
        var result = await response.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Mewtwo");
        result.Description.ShouldBe(yodaTranslation);
        result.IsLegendary.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTranslatedPokemon_ForCavePokemon_ReturnsYodaTranslation()
    {
        // Arrange
        var pokemon = PokemonRace.Create(41, "Zubat", "Cave dwelling bat", "cave", false).Value;
        var yodaTranslation = "Cave dwelling bat, it is";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Zubat"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Cave dwelling bat", TranslationType.Yoda, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(yodaTranslation));

        // Act
        var response = await _client.GetAsync("/Pokemon/translated/Zubat");
        var result = await response.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Description.ShouldBe(yodaTranslation);
    }

    [Fact]
    public async Task GetTranslatedPokemon_ForNormalPokemon_ReturnsShakespeareTranslation()
    {
        // Arrange
        var pokemon = PokemonRace.Create(25, "Pikachu", "Electric mouse", "forest", false).Value;
        var shakespeareTranslation = "Hark! An electric mouse";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Pikachu"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Electric mouse", TranslationType.Shakespeare, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(shakespeareTranslation));

        // Act
        var response = await _client.GetAsync("/Pokemon/translated/Pikachu");
        var result = await response.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Description.ShouldBe(shakespeareTranslation);
    }

    [Fact]
    public async Task GetPokemon_WhenNotFound_Returns404()
    {
        // Arrange
        var error = new NotFoundError("Pokemon not found");

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("NonExistent"))
            .ReturnsAsync(Result<PokemonRace>.Failure(error));

        // Act
        var response = await _client.GetAsync("/Pokemon/NonExistent");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTranslatedPokemon_WhenTranslationFails_ReturnsPokemonWithOriginalDescription()
    {
        // Arrange
        var pokemon = PokemonRace.Create(7, "Squirtle", "Water turtle", "water", false).Value;

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Squirtle"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync(It.IsAny<string>(), It.IsAny<TranslationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(new PersistenceError("Translation failed")));

        // Act
        var response = await _client.GetAsync("/Pokemon/translated/Squirtle");
        var result = await response.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Description.ShouldBe("Water turtle"); // Original description
    }

    [Fact]
    public async Task GetTranslatedPokemon_WhenTranslationCached_DoesNotCallTranslationAPI()
    {
        // Arrange
        var pokemon = PokemonRace.Create(6, "Charizard", "Fire dragon", "mountain", false).Value;
        var translation = "Hark! A fire dragon";

        _pokemonClientMock
            .Setup(x => x.GetPokemonRaceAsync("Charizard"))
            .ReturnsAsync(Result<PokemonRace>.Success(pokemon));

        _translatorClientMock
            .Setup(x => x.GetTranslationAsync("Fire dragon", TranslationType.Shakespeare, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(translation));

        // Act - First call
        var response1 = await _client.GetAsync("/Pokemon/translated/Charizard");

        // Act - Second call (should use cached translation)
        var response2 = await _client.GetAsync("/Pokemon/translated/Charizard");
        var result2 = await response2.Content.ReadFromJsonAsync<PokemonResult>();

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        result2!.Description.ShouldBe(translation);

        // Verify translation API called only once
        _translatorClientMock.Verify(
            x => x.GetTranslationAsync(It.IsAny<string>(), It.IsAny<TranslationType>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}