using FastEndpoints;
using FluentAssertions.Specialized;

namespace Endatix.Api.Tests.TestExtensions;

/// <summary>
/// Provides extension methods for various FluentAssertions methods, so that test code will be crisper and quicker to write
/// </summary>
internal static class FluentAssertionsЕxtensions
{
    /// <summary>
    /// Asserts that the function throws a ValidationFailureException with the expected message.
    /// </summary>
    /// <param name="assertions">The NonGenericAsyncFunctionAssertions instance.</param>
    /// <param name="expectedMessage">The expected error message.</param>
    /// <returns>An ExceptionAssertions{ValidationFailureException} for further assertions.</returns>
    internal static async Task<ExceptionAssertions<ValidationFailureException>> ThrowValidationFailureAsync(
               this NonGenericAsyncFunctionAssertions assertions,
               string expectedMessage)
    {
        return await assertions.ThrowAsync<ValidationFailureException>()
            .WithMessage($"ThrowError() called! - {expectedMessage}")
            .Where(ex => ex.Message.Contains(expectedMessage));
    }
}