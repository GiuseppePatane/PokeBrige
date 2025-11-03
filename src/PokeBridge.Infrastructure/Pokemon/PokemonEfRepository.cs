using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using PokeBridge.Infrastructure.EF;

namespace PokeBridge.Infrastructure.Pokemon;

/// <summary>
/// Entity Framework implementation of pokemon repository
/// Handles persistence and retrieval of pokemon data
/// </summary>
public class PokemonEfRepository : IPokemonRepository
{
    private readonly ILogger<PokemonEfRepository> _logger;
    private readonly PokeBridgeDbContext _dbContext;

    public PokemonEfRepository(ILogger<PokemonEfRepository> logger, PokeBridgeDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retrieves a pokemon by name (case-insensitive)
    /// </summary>
    /// <param name="name">Pokemon name to search for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pokemon race if found, error otherwise</returns>
    public async Task<Result<PokemonRace>> GetByName(string name, CancellationToken ct = default)
    {
        try
        {
            
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("GetByName called with null or empty name");
                return Result<PokemonRace>.Failure(
                    new ValidationError(nameof(name), "Name cannot be null or empty"));
            }
            
            var normalizedName = name.Trim().ToLowerInvariant();

            _logger.LogDebug("Searching for pokemon with name {PokemonName}", normalizedName);

            
            var pokemon = await _dbContext.PokemonRaces
                .AsNoTracking()  
                .FirstOrDefaultAsync(p => p.Name.ToLower() == normalizedName, ct);

            if (pokemon == null)
            {
                _logger.LogInformation(
                    "Pokemon with name {PokemonName} not found in database",
                    normalizedName);

                return Result<PokemonRace>.Failure(
                    new NotFoundError($"No pokemon found with name '{name}'"));
            }

            _logger.LogDebug(
                "Pokemon {PokemonName} (ID: {PokemonId}) retrieved from database",
                pokemon.Name,
                pokemon.Id);

            return Result<PokemonRace>.Success(pokemon);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GetByName operation cancelled for pokemon {PokemonName}", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Database error occurred while retrieving pokemon with name {PokemonName}",
                name);

            return Result<PokemonRace>.Failure(
                new PersistenceError("An error occurred while accessing the database"));
        }
    }

    /// <summary>
    /// Saves or updates a pokemon race
    /// If a pokemon with the same ID exists, it will be updated
    /// Otherwise, a new record will be created
    /// </summary>
    /// <param name="race">Pokemon race to save</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success result with saved pokemon, or failure</returns>
    public async Task<Result<PokemonRace>> Save(PokemonRace race, CancellationToken ct = default)
    {
        try
        {
            // Validate input
            if (race == null)
            {
                _logger.LogWarning("Save called with null pokemon race");
                return Result<PokemonRace>.Failure(
                    new ValidationError(nameof(race), "Pokemon race cannot be null"));
            }

            _logger.LogDebug(
                "Saving pokemon {PokemonName} (ID: {PokemonId})",
                race.Name,
                race.Id);

            // Check if pokemon already exists by ID (primary key - most efficient)
            var existingRace = await _dbContext.PokemonRaces
                .FirstOrDefaultAsync(p => p.Id == race.Id, ct);

            if (existingRace != null)
            {
                // Update existing pokemon
                _logger.LogDebug(
                    "Updating existing pokemon {PokemonName} (ID: {PokemonId})",
                    race.Name,
                    race.Id);

            
                _dbContext.Entry(existingRace).CurrentValues.SetValues(race);
                

                _logger.LogInformation(
                    "Pokemon {PokemonName} (ID: {PokemonId}) updated in database",
                    race.Name,
                    race.Id);
            }
            else
            {
                _logger.LogDebug(
                    "Inserting new pokemon {PokemonName} (ID: {PokemonId})",
                    race.Name,
                    race.Id);

                await _dbContext.PokemonRaces.AddAsync(race, ct);

                _logger.LogInformation(
                    "New pokemon {PokemonName} (ID: {PokemonId}) added to database",
                    race.Name,
                    race.Id);
            }
            
            var affectedRows = await _dbContext.SaveChangesAsync(ct);

            _logger.LogDebug(
                "SaveChanges completed, {AffectedRows} row(s) affected",
                affectedRows);

            return Result<PokemonRace>.Success(race);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrency conflict while saving pokemon {PokemonName} (ID: {PokemonId})",
                race?.Name ?? "unknown",
                race?.Id ?? 0);

            return Result<PokemonRace>.Failure(
                new PersistenceError("The pokemon was modified by another process. Please try again."));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database update error while saving pokemon {PokemonName} (ID: {PokemonId})",
                race?.Name ?? "unknown",
                race?.Id ?? 0);

             //check for duplicate name error
            if (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Result<PokemonRace>.Failure(
                    new PersistenceError($"A pokemon with name '{race?.Name}' already exists"));
            }

            return Result<PokemonRace>.Failure(
                new PersistenceError("An error occurred while saving to the database"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Save operation cancelled for pokemon {PokemonName}",
                race?.Name ?? "unknown");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while saving pokemon {PokemonName} (ID: {PokemonId})",
                race?.Name ?? "unknown",
                race?.Id ?? 0);

            return Result<PokemonRace>.Failure(
                new PersistenceError("An unexpected error occurred while saving to the database"));
        }
    }
}