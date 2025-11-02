using OneOf;

namespace PokeBridge.Core.Shared;

/// <summary>
/// 
/// </summary>
/// <typeparam name="TSuccess"></typeparam>
public class Result<TSuccess> : OneOfBase<TSuccess, DomainError>
{
    protected Result(OneOf<TSuccess, DomainError> input) : base(input) { }

    /// <summary>
    /// Create a success result
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Result<TSuccess> Success(TSuccess value) => new(value);

    /// <summary>
    /// Create a failure result
    /// </summary>
    public static Result<TSuccess> Failure(DomainError error) => new(error);

    /// <summary>
    ///  Checks if the result is a success
    /// </summary>
    public bool IsSuccess => IsT0;

    /// <summary>
    /// Checks if the result is a failure
    /// </summary>
    public bool IsFailure => IsT1;

    /// <summary>
    /// Gets the success value 
    /// </summary>
    public new TSuccess Value => AsT0;

    /// <summary>
    /// Gets the error 
    /// </summary>
    public DomainError Error => AsT1;
}


/// <summary>
/// Creates a result type for operations that do not return a value on success
/// </summary>
public class Result : OneOfBase<Success, DomainError>
{
    protected Result(OneOf<Success, DomainError> input) : base(input) { }

    /// <summary>
    /// Create a success result
    /// </summary>
    public static Result Ok() => new(new Success());

    /// <summary>
    /// Create a failure result
    /// </summary>
    public static Result Failure(DomainError error) => new(error);

    /// <summary>
    /// Checks if the result is a success
    /// </summary>
    public bool IsSuccess => IsT0;

    /// <summary>
    /// Checks if the result is a failure
    /// </summary>
    public bool IsFailure => IsT1;

    /// <summary>
    /// Gets the error
    /// </summary>
    public DomainError Error => AsT1;
}

/// <summary>
/// Marker type for success without value
/// </summary>
public readonly record struct Success;