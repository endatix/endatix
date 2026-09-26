using Endatix.Core.Exceptions;

namespace Endatix.Core.Tests.Exceptions;

public sealed class DomainValidationExceptionTests
{
    [Fact]
    public void ThrowIfError_NullError_DoesNotThrow()
    {
        var act = () => DomainValidationException.ThrowIfError(null, "email");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfError_Message_ThrowsWithEndUserMessageAndParamName()
    {
        var act = () => DomainValidationException.ThrowIfError("Email is required.", "email");

        var ex = act.Should().Throw<DomainValidationException>().Which;
        ex.EndUserMessage.Should().Be("Email is required.");
        ex.ParamName.Should().Be("email");
    }
}
