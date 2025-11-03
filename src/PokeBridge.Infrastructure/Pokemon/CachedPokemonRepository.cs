using Microsoft.Extensions.Logging;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using ZiggyCreatures.Caching.Fusion;

namespace PokeBridge.Infrastructure.Pokemon;

/// <summary>
/// Decorator that adds caching capabilities to pokemon repository.
/// </summary>
public class CachedPokemonRepository : IPokemonRepository
{
    private readonly IPokemonRepository _innerRepository;
    private readonly IFusionCache _cache;
    private readonly ILogger<CachedPokemonRepository> _logger;
    
    private static class CacheConfig
    {
        
        public const string PokemonByNameKeyPattern = "pokemon:name:{0}";
        public const string PokemonByIdKeyPattern = "pokemon:id:{0}";

        // TTL (Time To Live) configuration
        // Pokemon data is mostly immutable, so we can cache for a long time. I hope :)
        public static readonly TimeSpan MemoryCacheDuration = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DistributedCacheDuration = TimeSpan.FromHours(24);

        // Fail-safe: keep stale cache if DB is down 
        // see: https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md
        public static readonly TimeSpan FailSafeDuration = TimeSpan.FromDays(7);
    }

    public CachedPokemonRepository(
        IPokemonRepository innerRepository,
        IFusionCache cache,
        ILogger<CachedPokemonRepository> logger)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets pokemon by name with caching
    /// Cache key: pokemon:name:{normalizedName}
    /// </summary>
    public async Task<Result<PokemonRace>> GetByName(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            // inner repository in charge of validation and error handling.
            return await _innerRepository.GetByName(name, ct);
        }
        
        var normalizedName = name.Trim().ToLowerInvariant();
        var cacheKey = string.Format(CacheConfig.PokemonByNameKeyPattern, normalizedName);

        _logger.LogDebug("Attempting to retrieve pokemon {PokemonName} from cache", normalizedName);

        try
        {
            var cachedPokemon = await _cache.GetOrSetAsync<PokemonRace?>(
                cacheKey,
                async (ctx, token) =>
                {
                    _logger.LogDebug(
                        "Cache MISS for pokemon {PokemonName} - fetching from repository",
                        normalizedName);

                    var repoResult = await _innerRepository.GetByName(name, token);

                    if (repoResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "Repository returned error for pokemon {PokemonName}: {ErrorMessage} - not caching result",
                            normalizedName,
                            repoResult.Error.Message);
                        // Skip caching on failure
                       throw new Exception(repoResult.Error.Message);
                    }

                    return repoResult.Value;
                },
                options => options
                    .SetDuration(CacheConfig.MemoryCacheDuration)
                    .SetDistributedCacheDuration(CacheConfig.DistributedCacheDuration)
                    .SetFailSafe(true, CacheConfig.FailSafeDuration)
                    .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                ct
            );

            if (cachedPokemon != null)
            {
                _logger.LogDebug(
                    "Cache HIT for pokemon {PokemonName} (ID: {PokemonId})",
                    cachedPokemon.Name,
                    cachedPokemon.Id);

                return Result<PokemonRace>.Success(cachedPokemon);
            }
            
            _logger.LogDebug("Cache returned null, fetching from repository to get error details");
            return await _innerRepository.GetByName(name, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error accessing cache for pokemon {PokemonName}, falling back to repository",
                normalizedName);

            // On cache error, fallback to repository
            return await _innerRepository.GetByName(name, ct);
        }
    }

    /// <summary>
    /// Saves pokemon and invalidates related cache entries
    /// </summary>
    public async Task<Result<PokemonRace>> Save(PokemonRace race, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Saving pokemon {PokemonName} (ID: {PokemonId}) and invalidating cache",
            race.Name,
            race.Id);
        
        var result = await _innerRepository.Save(race, ct);

        if (result.IsSuccess)
        {
            await InvalidatePokemonCache(race.Name, race.Id);
            _logger.LogInformation(
                "Pokemon {PokemonName} (ID: {PokemonId}) saved and cache invalidated",
                race.Name,
                race.Id);
        }
        else
        {
            _logger.LogWarning(
                "Failed to save pokemon {PokemonName}, cache not invalidated",
                race.Name);
        }

        return result;
    }

    /// <summary>
    /// Invalidates all cache entries for a specific pokemon
    /// </summary>
    private async Task InvalidatePokemonCache(string pokemonName, int pokemonId)
    {
        if (string.IsNullOrWhiteSpace(pokemonName))
        {
            return;
        }

        var normalizedName = pokemonName.Trim().ToLowerInvariant();
        
        var cacheKeyByName = string.Format(CacheConfig.PokemonByNameKeyPattern, normalizedName);
        var cacheKeyById = string.Format(CacheConfig.PokemonByIdKeyPattern, pokemonId);

        try
        {
            await Task.WhenAll(
                _cache.RemoveAsync(cacheKeyByName).AsTask(),
                _cache.RemoveAsync(cacheKeyById).AsTask()
            );

            _logger.LogDebug(
                "Cache invalidated for pokemon {PokemonName} (ID: {PokemonId})",
                pokemonName,
                pokemonId);
        }
        catch (Exception ex)
        {
           // log only, do not fail the save operation
            _logger.LogWarning(
                ex,
                "Failed to invalidate cache for pokemon {PokemonName} (ID: {PokemonId})",
                pokemonName,
                pokemonId);
        }
    }
}