using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobTypeDefaultsTests
{
    [Fact]
    public void Ctor_DefaultsDeclaredTwiceForOneJobType_ThrowsNamingIt()
    {
        // Arrange
        var declare = () => DeclaredDefaults.For(DeclaredDefaults.Tuned, "WebHookDelivery", "WebHookDelivery");

        // Act
        var thrown = Record.Exception(declare);

        // Assert — two sets of settings for one job type would leave which one applies to registration order.
        thrown.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("'WebHookDelivery'");
    }

    [Fact]
    public void Ctor_DefaultsWithoutJobType_Throws()
    {
        // Arrange — registered as a plain singleton, so nothing ever set the job type.
        var declare = () => new JobTypeDefaults([DeclaredDefaults.Tuned]);

        // Act
        var thrown = Record.Exception(declare);

        // Assert — the handler registry refuses a blank job type, so no job could ever run with these settings.
        thrown.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("without a job type");
    }

    [Fact]
    public void For_JobTypeThatDeclaredNothing_ReturnsNull()
    {
        // Arrange
        var defaults = DeclaredDefaults.For(DeclaredDefaults.Tuned, "WebHookDelivery");

        // Act
        var undeclared = defaults.For("SubmissionExport");

        // Assert
        undeclared.Should().BeNull();
    }
}
