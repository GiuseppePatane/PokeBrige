namespace PokeBridge.Core.Shared;

public abstract record DomainError(string Code, string Message);

public record ValidationError(string FieldName, string Reason)
    : DomainError("VALIDATION_ERROR", $"Validation failed for '{FieldName}': {Reason}");

public record NotFoundError(string Details) : DomainError("POKEMON_RACE_NOT_FOUND", Details);

public record PersistenceError(string Details) : DomainError("PERSISTENCE_ERROR", Details);
