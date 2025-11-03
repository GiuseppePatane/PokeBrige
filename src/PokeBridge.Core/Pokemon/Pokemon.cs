using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Pokemon;

public class PokemonRace
{
    private PokemonRace(int id, string name, string description, string habitat, bool isLegendary)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        Id = id;
        Name = name;
        Description = description;
        Habitat = habitat;
        IsLegendary = isLegendary;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Habitat { get; set; }
    public bool IsLegendary { get; set; }
    public  Dictionary<TranslationType, string> Translations { get; } = new();
    public DateTime CreatedAt { get; private set; } 
    public DateTime? UpdatedAt { get; private set; }

    public static Result<PokemonRace> Create(int id, string name, string description, string habitat, bool isLegendary)
    {
        if (id <= 0)
            return Result<PokemonRace>.Failure(new ValidationError(nameof(id), "Id must be greater than zero"));
        
        if (string.IsNullOrWhiteSpace(name))
            return Result<PokemonRace>.Failure(new ValidationError(nameof(name), "Name cannot be empty"));
        
        var pokemonRace = new PokemonRace(id, name, description, habitat, isLegendary);
        return Result<PokemonRace>.Success(pokemonRace);
    }
    
    public Result AddTranslation(TranslationType type, string translatedDescription)
    {
        if (string.IsNullOrWhiteSpace(translatedDescription))
          return Result.Failure(new ValidationError(nameof(translatedDescription), "Translated description cannot be empty"));

        if(!Enum.IsDefined(type))
            return Result.Failure(new ValidationError(nameof(type), $"Invalid translation type: {type}"));

        Translations[type] = translatedDescription;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok();
    }
    
    public bool HasTranslation(TranslationType type)
    {
        return Translations.ContainsKey(type);
    }

    public PokemonResult ToPokemonResult(TranslationType translationType)
    {
       var description = Translations.GetValueOrDefault(translationType, Description);

        return new PokemonResult
        {
            Name = Name,
            Description = description,
            Habitat = Habitat,
            IsLegendary = IsLegendary
        };
    }
}

public record PokemonNotFoundError(string PokemonName)
    : DomainError("POKEMON_NOT_FOUND", $"Pokemon '{PokemonName}' was not found");