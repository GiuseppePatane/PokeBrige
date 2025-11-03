using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
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
        services.AddExternalService(configuration);
        services.AddDatabase(configuration);
        services.AddRepositories();
        services.AddCache(configuration);
        services.AddDomainService();
        return services;
    }

    private static IServiceCollection AddDomainService(this IServiceCollection services)
    {
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

    private static IServiceCollection AddExternalService (this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPokemonClient, PokeClient>();
        services.AddTransient<PokeApiClient>();
        services.AddHttpClient<ITranslatorClient,TranslatorHttpClient>(configuration["HttpClients:FunTranslationsApiBaseUrl"] ??
                                                                       throw new InvalidOperationException());
        return services;
    }

    private static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
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
            .WithDistributedCache(
                new RedisCache(new RedisCacheOptions
                {
                    Configuration = configuration["Redis:Configuration"]
                })
            )
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = configuration["Redis:Configuration"]
            }));

        return services;
    }
    
    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                configuration.GetConnectionString("PokeBridgeDatabase") ??
                throw new InvalidOperationException());
            dataSourceBuilder.EnableDynamicJson();
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<PokeBridgeDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource);
        });
        
        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<PokemonEfRepository>();
        
        services.AddScoped<IPokemonRepository>(serviceProvider =>
        {
            var innerRepository = serviceProvider.GetRequiredService<PokemonEfRepository>();
            var cache = serviceProvider.GetRequiredService<IFusionCache>();
            var logger = serviceProvider.GetRequiredService<ILogger<CachedPokemonRepository>>();

            return new CachedPokemonRepository(innerRepository, cache, logger);
        });

        return services;
    }
}
