using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Features.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class CompileFormSchemaTenantScopeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Handle_WhenFormOutsidePositiveTenantScope_ReturnsNotFound(long requestTenant)
    {
        // Arrange
        var form = Form.Create(100, new FormCreateArgs(TenantId: 1, Name: "Report"));
        var forms = Substitute.For<IRepository<Form>>();
        forms.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ActiveFormDefinitionByFormIdSpec>().Evaluate([form]).SingleOrDefault());
        var handler = new CompileFormSchemaHandler(forms, Substitute.For<IFormSchemaProcessor>());

        // Act
        var result = await handler.Handle(new CompileFormSchemaCommand(form.Id, requestTenant), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().ContainSingle().Which.Should().Be("Form not found.");
    }
}
