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

    /// ef core constructor
    private PokemonRace(){}
    public int Id { get;  private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Habitat { get; private set; }
    public bool IsLegendary { get;  private set; }
    public  Dictionary<TranslationType, string> Translations { get; private set; } = new();
    public DateTime CreatedAt { get; private set; } 
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    ///  Creates a new PokemonRace instance.
    /// </summary>
    /// <param name="id">the unique identifier for the pokemon race</param>
    /// <param name="name">the name of the pokemon race</param>
    /// <param name="description">the description of the pokemon race</param>
    /// <param name="habitat">the habitat of the pokemon race</param>
    /// <param name="isLegendary">indicates if the pokemon race is legendary</param>
    /// <returns></returns>
    public static Result<PokemonRace> Create(int id, string name, string description, string habitat, bool isLegendary)
    {
        if (id <= 0)
            return Result<PokemonRace>.Failure(new ValidationError(nameof(id), "Id must be greater than zero"));
        
        if (string.IsNullOrWhiteSpace(name))
            return Result<PokemonRace>.Failure(new ValidationError(nameof(name), "Name cannot be empty"));
        
        var pokemonRace = new PokemonRace(id, name, description, habitat, isLegendary);
        return Result<PokemonRace>.Success(pokemonRace);
    }
    
    /// <summary>
    ///  Adds a translation for the pokemon description.
    /// </summary>
    /// <param name="type">the type of translation <see cref="TranslationType"/>></param>
    /// <param name="translatedDescription">the translated description</param>
    /// <returns> the result of the operation</returns>
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
    
    /// <summary>
    ///  Checks if a translation exists for the given type.
    /// </summary>
    /// <param name="type">the type of translation <see cref="TranslationType"/>></param>
    /// <returns> true if a translation exists, false otherwise</returns>
    public bool HasTranslation(TranslationType type)
    {
        return Translations.ContainsKey(type);
    }

    /// <summary>
    ///  Converts the PokemonRace to a PokemonResult with the specified translation type.
    /// </summary>
    /// <param name="translationType">the type of translation to use</param>
    /// <returns> the PokemonResult</returns>
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

    /// <summary>
    ///  Updates the current PokemonRace instance with values from another instance.
    /// </summary>
    /// <param name="race">the PokemonRace instance to copy values from</param>
    /// <exception cref="ArgumentNullException"> thrown if the race is null</exception>
    public void UpdateFrom(PokemonRace race)
    {
        if (race == null) throw new ArgumentNullException(nameof(race));
        Description = race.Description;
        Habitat = race.Habitat;   
        IsLegendary = race.IsLegendary;
        Translations = race.Translations;
        UpdatedAt = DateTime.UtcNow;    
    }
}
