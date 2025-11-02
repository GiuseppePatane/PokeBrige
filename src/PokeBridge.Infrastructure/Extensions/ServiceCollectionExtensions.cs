using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PokeApiNet;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Translator;
using PokeBridge.Infrastructure.Pokemon;
using PokeBridge.Infrastructure.Translator;

namespace PokeBridge.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,string baseAddress)
    {

        services.AddScoped<IPokemonClient, PokeClient>();
      
        services.AddScoped<PokemonService>();
        services.AddTransient<PokeApiClient>();
        services.AddHttpClients(baseAddress);
        
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