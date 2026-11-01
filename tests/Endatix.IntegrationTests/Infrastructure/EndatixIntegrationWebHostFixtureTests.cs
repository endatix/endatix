namespace Endatix.IntegrationTests;

/// <summary>
/// xUnit disposes a collection fixture whose initialization failed or never ran, and that disposal must not throw a
/// failure of its own in place of the one the run reports.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class EndatixIntegrationWebHostFixtureTests
{
    [Fact]
    public async Task A_fixture_that_never_initialized_disposes_without_throwing()
    {
        // Arrange — constructing the fixture reads its settings only; nothing is started until it initializes.
        var fixture = new EndatixIntegrationWebHostFixture();

        // Act
        var disposalFailure = await Record.ExceptionAsync(() => fixture.DisposeAsync().AsTask());

        // Assert
        Assert.Null(disposalFailure);
    }
}
