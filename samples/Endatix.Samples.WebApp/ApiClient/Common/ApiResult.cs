namespace Endatix.Samples.WebApp.ApiClient.Common;

/// <summary>
/// Represents the result of an API operation, which can be either a successful value or an error.
/// </summary>
/// <typeparam name="TValue">The type of the value returned in case of success.</typeparam>
public readonly struct ApiResult<TValue>
{
    private readonly TValue? _value;

    private readonly ApiError? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResult{TValue}"/> struct with a successful value.
    /// </summary>
    /// <param name="value">The successful value.</param>
    private ApiResult(TValue value)
    {
        if (typeof(TValue) == typeof(ApiError))
        {
            throw new ArgumentException("TValue cannot be of type ApiError.");
        }

        IsError = false;
        _value = value;
        _error = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResult{TValue}"/> struct with an error.
    /// </summary>
    /// <param name="error">The error.</param>
    private ApiResult(ApiError error)
    {
        IsError = true;
        _error = error;
        _value = default;
    }

    /// <summary>
    /// Gets a value indicating whether the result is an error.
    /// </summary>
    public bool IsError { get; }

    /// <summary>
    /// Gets a value indicating whether the result is a success.
    /// </summary>
    public bool IsSuccess => !IsError;

    /// <summary>
    /// Implicitly converts a successful value to an <see cref="ApiResult{TValue}"/>.
    /// </summary>
    /// <param name="value">The successful value.</param>
    /// <returns>An <see cref="ApiResult{TValue}"/> representing the successful value.</returns>
    public static implicit operator ApiResult<TValue>(TValue value) => new(value);

    /// <summary>
    /// Implicitly converts an error to an <see cref="ApiResult{TValue}"/>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>An <see cref="ApiResult{TValue}"/> representing the error.</returns>
    public static implicit operator ApiResult<TValue>(ApiError error) => new(error);

    /// <summary>
    /// Matches the result with either a success or error handler.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the match operation.</typeparam>
    /// <param name="onSuccess">The action to perform if the result is a success.</param>
    /// <param name="onError">The action to perform if the result is an error.</param>
    /// <returns>The result of the match operation.</returns>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<ApiError, TResult> onError) =>
        IsError ? onError(_error!) : onSuccess(_value!);

    /// <summary>
    /// Matches the result with either a success or error handler without returning a value.
    /// </summary>
    /// <param name="onSuccess">The action to perform if the result is a success.</param>
    /// <param name="onError">The action to perform if the result is an error.</param>
    public void Match(Action<TValue> onSuccess, Action<ApiError> onError)
    {
        if (IsError)
        {
            onError(_error!);
        }
        else
        {
            onSuccess(_value!);
        }
    }
}