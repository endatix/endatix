using Endatix.Core.Features.WebHooks;

namespace Endatix.Core.Tests.Features.WebHooks
{
    public class ActionNamesTests
    {
        [Theory]
        [InlineData(ActionName.Created, "created")]
        [InlineData(ActionName.Updated, "updated")]
        [InlineData(ActionName.Deleted, "deleted")]
        public void KnownActionNames_ShouldHaveCorrectDisplayNames(ActionName actionName, string expectedDisplayName)
        {
            // Act
            var displayName = actionName.GetDisplayName();

            // Assert
            displayName.Should().Be(expectedDisplayName);
        }
    }
}