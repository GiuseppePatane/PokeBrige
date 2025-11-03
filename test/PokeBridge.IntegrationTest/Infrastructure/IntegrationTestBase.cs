using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PokeBridge.Infrastructure.EF;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace PokeBridge.IntegrationTest.Infrastructure;

/// <summary>
/// Base class for integration tests using real database and cache via Testcontainers
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;

    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected PokeBridgeDbContext DbContext { get; private set; } = null!;
    protected IFusionCache Cache { get; private set; } = null!;

    protected IntegrationTestBase()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("pokebridgetest")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start containers in parallel
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync()
        );

        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

       
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                _postgresContainer.GetConnectionString());
            dataSourceBuilder.EnableDynamicJson();
            return dataSourceBuilder.Build();
        });
        
        services.AddDbContext<PokeBridgeDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource);
            options.EnableSensitiveDataLogging();
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));
        });

        // Redis distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = $"{_redisContainer.GetConnectionString()},allowAdmin=true";
            options.InstanceName = "PokeBridgeTest_";
        });

        // FusionCache with distributed cache
        services.AddFusionCache()
            .WithDefaultEntryOptions(options => options
                .SetDuration(TimeSpan.FromMinutes(5))
                .SetDistributedCacheDuration(TimeSpan.FromHours(1))
                .SetFailSafe(true))
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(
                new RedisCache(new RedisCacheOptions
                {
                    Configuration = $"{_redisContainer.GetConnectionString()},allowAdmin=true"
                })
            );
                

        // Allow derived classes to add additional services
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        // Initialize database
        DbContext = ServiceProvider.GetRequiredService<PokeBridgeDbContext>();
        await DbContext.Database.EnsureCreatedAsync();

        Cache = ServiceProvider.GetRequiredService<IFusionCache>();

        await SeedDataAsync();
    }

    public async Task DisposeAsync()
    {
     
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask()
        );
    }

    /// <summary>
    /// Override to configure additional services
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
       
    }

    /// <summary>
    /// Override to seed test data
    /// </summary>
    protected virtual Task SeedDataAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clear all cache entries
    /// </summary>
    protected async Task ClearCacheAsync()
    {
        var options = ServiceProvider.GetRequiredService<IOptions<RedisCacheOptions>>();
        var connection = await ConnectionMultiplexer.ConnectAsync(options.Value.Configuration!);
        var server = connection.GetServer(connection.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }

    /// <summary>
    /// Clear all database entries
    /// </summary>
    protected async Task ClearDatabaseAsync()
    {
        DbContext.PokemonRaces.RemoveRange(DbContext.PokemonRaces);
        await DbContext.SaveChangesAsync();
    }
}