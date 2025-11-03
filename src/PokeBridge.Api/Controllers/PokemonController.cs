using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PokeBridge.Core;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;

namespace PokeBridge.Api.Controllers;

/// <summary>
/// Controller for pokemon-related operations
/// </summary>
[ApiController]
[Route("[controller]")]
public class PokemonController : ControllerBase
{
    private readonly ILogger<PokemonController> _logger;
    private readonly PokemonService _pokemonService;

    public PokemonController(ILogger<PokemonController> logger, PokemonService pokemonService)
    {
        _logger = logger;
        _pokemonService = pokemonService;
    }

    /// <summary>
    /// Gets basic pokemon information
    /// </summary>
    /// <param name="name">Pokemon name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pokemon information with original description</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(PokemonResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPokemonInfo(
        [FromRoute][Required][StringLength(100, MinimumLength = 1)] string name,
        CancellationToken cancellationToken)
    {
        var result = await _pokemonService.GetPokemon(name, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        _logger.LogWarning(
            "Failed to retrieve pokemon {PokemonName}: {ErrorMessage}",
            name,
            result.Error.Message);

        return MapErrorToProblemDetails(result.Error);
    }

    /// <summary>
    /// Gets pokemon information with translated description
    /// Translation type is automatically selected based on pokemon characteristics
    /// </summary>
    /// <param name="name">Pokemon name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pokemon information with translated description</returns>
    [HttpGet("translated/{name}")]
    [ProducesResponseType(typeof(PokemonResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTranslatedPokemonInfo(
        [FromRoute][Required][StringLength(100, MinimumLength = 1)] string name,
        CancellationToken cancellationToken)
    {
        var result = await _pokemonService.GetTranslatedPokemonRace(name, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        _logger.LogWarning(
            "Failed to retrieve translated pokemon {PokemonName}: {ErrorMessage}",
            name,
            result.Error.Message);

        return MapErrorToProblemDetails(result.Error);
    }
    
    
     private IActionResult MapErrorToProblemDetails(DomainError error)
    {
        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Extensions = { ["errorCode"] = error.Code },
        };
        

        var (statusCode, type) = error.Code switch
        {
            "POKEMON_RACE_NOT_FOUND" => (
                StatusCodes.Status404NotFound,
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4"
            ),
            "TRANSLATION_NOT_SUPPORTED" or
            "POKEMON_API_ERROR"
                => (
                StatusCodes.Status400BadRequest,
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
            ),
        };

        problemDetails.Status = statusCode;
        problemDetails.Type = type;

        _logger.LogWarning(
            "Returning error response: {Code} with HTTP status {StatusCode}",
            error.Code,
            statusCode
        );

        return StatusCode(statusCode, problemDetails);
    }
}