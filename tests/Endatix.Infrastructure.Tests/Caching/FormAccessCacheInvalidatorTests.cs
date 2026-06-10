using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Caching;

public sealed class FormAccessCacheInvalidatorTests
{
    private const long FormId = 42;

    [Fact]
    public async Task InvalidateFormAsync_CallsRemoveByTagAsync_WithFormTag()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        var invalidator = new FormAccessCacheInvalidator(cache, NullLogger<FormAccessCacheInvalidator>.Instance);

        // Act
        await invalidator.InvalidateFormAsync(FormId, TestContext.Current.CancellationToken);

        // Assert
        await cache.Received(1).RemoveByTagAsync(
            Arg.Is<IEnumerable<string>>(tags => tags.SequenceEqual(new[] { FormAccessCacheTags.ForForm(FormId) })),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidateFormAsync_WhenCacheThrowsNonCancellationException_LogsWarningAndDoesNotRethrow()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        cache
            .RemoveByTagAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("Redis unavailable")));

        var logger = Substitute.For<ILogger<FormAccessCacheInvalidator>>();
        var invalidator = new FormAccessCacheInvalidator(cache, logger);

        // Act
        var act = () => invalidator.InvalidateFormAsync(FormId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(e => e is InvalidOperationException),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvalidateFormAsync_WhenOperationCanceledException_Propagates()
    {
        // Arrange
        var cache = Substitute.For<HybridCache>();
        cache
            .RemoveByTagAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new OperationCanceledException()));

        var invalidator = new FormAccessCacheInvalidator(cache, NullLogger<FormAccessCacheInvalidator>.Instance);

        // Act
        var act = () => invalidator.InvalidateFormAsync(FormId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FormUpdatedHandler_CallsInvalidateFormAsync_WithFormId()
    {
        // Arrange
        var invalidator = Substitute.For<IFormAccessCacheInvalidator>();
        var handler = new InvalidateFormAccessCacheOnFormUpdatedHandler(invalidator);
        var form = new Form(1, "Test form", isPublic: false);
        var notification = new FormUpdatedEvent(form);

        // Act
        await handler.Handle(notification, TestContext.Current.CancellationToken);

        // Assert
        await invalidator.Received(1).InvalidateFormAsync(form.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FormDeletedHandler_CallsInvalidateFormAsync_WithFormId()
    {
        // Arrange
        var invalidator = Substitute.For<IFormAccessCacheInvalidator>();
        var handler = new InvalidateFormAccessCacheOnFormDeletedHandler(invalidator);
        var form = new Form(1, "Test form", isPublic: false);
        var notification = new FormDeletedEvent(form);

        // Act
        await handler.Handle(notification, TestContext.Current.CancellationToken);

        // Assert
        await invalidator.Received(1).InvalidateFormAsync(form.Id, TestContext.Current.CancellationToken);
    }
}
