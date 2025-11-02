using Microsoft.AspNetCore.Mvc;
using PokeBridge.Core.Pokemon;
using PokeBridge.Core.Shared;
using PokeBridge.Core.Translator;

namespace PokeBridge.Api.Controllers;

public class PokemonController : Controller
{
    private readonly ILogger<PokemonController> _logger;
    private readonly PokemonService _pokemonService;
    public PokemonController(ILogger<PokemonController> logger, PokemonService pokemonService)
    {
        _logger = logger;
        _pokemonService = pokemonService;
    }
    [HttpGet("pokemon/{name}")]
    public async Task<IActionResult> GetPokemonInfo(string name)
    {
        var result = await _pokemonService.GetPokemonRace(name);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        _logger.LogError("Error retrieving pokemon info: {ErrorMessage}", result.Error.Message);
        return  MapErrorToProblemDetails(result.Error);  
    }
    [HttpGet("pokemon/translated/{name}")]
    public async Task<IActionResult> GetTranslatedPokemonInfo(string name)
    {
        var result = await _pokemonService.GetTranslatedPokemonRace(name);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        _logger.LogError("Error retrieving translated pokemon info: {ErrorMessage}", result.Error.Message);
        return MapErrorToProblemDetails(result.Error);  
    }
    
    
     protected IActionResult MapErrorToProblemDetails(DomainError error)
    {
        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Extensions = { ["errorCode"] = error.Code },
        };
        

        var (statusCode, type) = error.Code switch
        {
            "POKEMON_NOT_FOUND" => (
                StatusCodes.Status404NotFound,
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4"
            ),
            "TRANSLATION_NOT_SUPPORTED" => (
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