namespace PokeBridge.Core.Shared;

public abstract record DomainError(string Code, string Message);


    
public record ValidationError(string FieldName, string Reason)
    : DomainError("VALIDATION_ERROR", $"Validation failed for '{FieldName}': {Reason}");