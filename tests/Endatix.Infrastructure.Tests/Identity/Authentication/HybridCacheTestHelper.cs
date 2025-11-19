using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute.Core;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

/// <summary>
/// Helper class to simplify mocking HybridCache in unit tests.
/// Based on the pattern from https://github.com/dotnet/extensions/issues/5763
/// </summary>
public static class HybridCacheTestHelper
{
    /// <summary>
    /// Holder class to capture values from mock calls.
    /// </summary>
    public sealed class CaptureHolder<T>
    {
        public T? Value { get; set; }
    }
    /// <summary>
    /// Sets up the HybridCache mock to return a specific Result&lt;AuthorizationData&gt; when GetOrCreateAsync is called.
    /// The factory function will be executed to get the actual result.
    /// </summary>
    public static ConfiguredCall SetupGetOrCreateAsync(
        this HybridCache mockCache,
        Result<AuthorizationData>? result = null)
    {
        return mockCache.GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                
                // Execute the factory - it returns ValueTask<AuthorizationData>
                // The factory is: async _ => await authorizationStrategy.GetAuthorizationDataAsync(...)
                // Which returns Result<AuthorizationData>, but is implicitly converted to AuthorizationData
                var valueTask = factory(cancellationToken);
                
                // If a specific result was provided, extract its value; otherwise get the factory result
                // The cache returns ValueTask<AuthorizationData>, not Result<AuthorizationData>
                if (result is not null)
                {
                    return new ValueTask<AuthorizationData>(result.Value);
                }
                
                // If the ValueTask is already completed, return it directly wrapped
                if (valueTask.IsCompletedSuccessfully)
                {
                    return new ValueTask<AuthorizationData>(valueTask.Result);
                }
                
                // For async operations, we need to complete them synchronously
                // This works because our factory (the strategy mock) returns synchronously
                var authData = valueTask.AsTask().GetAwaiter().GetResult();
                return new ValueTask<AuthorizationData>(authData);
            });
    }

    /// <summary>
    /// Sets up the HybridCache mock to capture the cache key when GetOrCreateAsync is called.
    /// Returns a holder object that contains the captured key.
    /// </summary>
    public static (ConfiguredCall Call, CaptureHolder<string> KeyHolder) SetupGetOrCreateAsyncWithKeyCapture(
        this HybridCache mockCache,
        Result<AuthorizationData>? result = null)
    {
        var keyHolder = new CaptureHolder<string>();
        var configuredCall = mockCache.GetOrCreateAsync(
            Arg.Do<string>(k => keyHolder.Value = k),
            Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                
                var valueTask = factory(cancellationToken);
                AuthorizationData authData;
                
                if (valueTask.IsCompletedSuccessfully)
                {
                    authData = valueTask.Result;
                }
                else
                {
                    authData = valueTask.AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                
                // The cache returns Result<AuthorizationData>
                return result ?? Result.Success(authData);
            });
        
        return (configuredCall, keyHolder);
    }

    /// <summary>
    /// Sets up the HybridCache mock to capture the cache options when GetOrCreateAsync is called.
    /// Returns a holder object that contains the captured options.
    /// </summary>
    public static (ConfiguredCall Call, CaptureHolder<HybridCacheEntryOptions> OptionsHolder) SetupGetOrCreateAsyncWithOptionsCapture(
        this HybridCache mockCache,
        Result<AuthorizationData>? result = null)
    {
        var optionsHolder = new CaptureHolder<HybridCacheEntryOptions>();
        var configuredCall = mockCache.GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, ValueTask<AuthorizationData>>>(),
            Arg.Do<HybridCacheEntryOptions?>(opt => optionsHolder.Value = opt),
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<AuthorizationData>>>();
                var cancellationToken = callInfo.Arg<CancellationToken>();
                
                var valueTask = factory(cancellationToken);
                AuthorizationData authData;
                
                if (valueTask.IsCompletedSuccessfully)
                {
                    authData = valueTask.Result;
                }
                else
                {
                    authData = valueTask.AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                
                // The cache returns Result<AuthorizationData>
                return result ?? Result.Success(authData);
            });
        
        return (configuredCall, optionsHolder);
    }
}

