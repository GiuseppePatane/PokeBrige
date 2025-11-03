using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PokeApiNet;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.EF;
using PokeBridge.Infrastructure.Pokemon;
using PokeBridge.Infrastructure.Translator;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace PokeBridge.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
    {
        // External API Clients
        services.AddScoped<IPokemonClient, PokeClient>();
        services.AddTransient<PokeApiClient>();
        services.AddHttpClients(configuration["HttpClients:FunTranslationsApiBaseUrl"] ?? throw new InvalidOperationException());

        // Database
        services.AddDbContext<PokeBridgeDbContext>(options =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                    configuration.GetConnectionString("PokeBridgeDatabase") ??
                    throw new InvalidOperationException());
                dataSourceBuilder.EnableDynamicJson();
                options.UseNpgsql(dataSourceBuilder.Build());
            }
        );

        // Caching (Redis + FusionCache)
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:Configuration"];
            options.InstanceName = configuration["Redis:InstanceName"];
        });

        services.AddFusionCache()
            .WithDefaultEntryOptions(options => options
                .SetDuration(TimeSpan.FromMinutes(5))
                .SetDistributedCacheDuration(TimeSpan.FromHours(1))
                .SetFailSafe(true))
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = configuration["Redis:Configuration"]
            }));

        // Repositories with Decorator Pattern
        // Register concrete repository first
        services.AddScoped<PokemonEfRepository>();

        // Decorate with caching layer
        services.AddScoped<IPokemonRepository>(serviceProvider =>
        {
            var innerRepository = serviceProvider.GetRequiredService<PokemonEfRepository>();
            var cache = serviceProvider.GetRequiredService<IFusionCache>();
            var logger = serviceProvider.GetRequiredService<ILogger<CachedPokemonRepository>>();

            return new CachedPokemonRepository(innerRepository, cache, logger);
        });

        // Domain Services
        services.AddScoped<ITranslationTypeSelector, TranslationTypeSelector>();
        services.AddScoped<ITranslationService, TranslationService>();
        services.AddScoped<PokemonService>();

        return services;
    }
    
    private static IServiceCollection AddHttpClients(this IServiceCollection services, string baseAddress)
    {
        services.AddHttpClient<ITranslatorClient,TranslatorHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        });
        return services;
    }
}