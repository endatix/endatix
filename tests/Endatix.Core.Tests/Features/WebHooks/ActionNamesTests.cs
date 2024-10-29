using Endatix.Core.Features.WebHooks;

namespace Endatix.Core.Tests.Features.WebHooks
{
    public class ActionNamesTests
    {
        [Theory]
        [InlineData(ActionNames.Created, "created")]
        [InlineData(ActionNames.Updated, "updated")]
        [InlineData(ActionNames.Deleted, "deleted")]
        public void KnownActionNames_ShouldHaveCorrectDisplayNames(ActionNames actionName, string expectedDisplayName)
        {
            // Act
            var displayName = actionName.GetDisplayName();

            // Assert
            displayName.Should().Be(expectedDisplayName);
        }
    }
}