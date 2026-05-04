using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Authorization.PublicForm;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Endatix.Infrastructure.Features.Authorization.PublicForm;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Authorization.PublicForm;

/// <summary>
/// Ensures mint uses <see cref="FormSpecifications.ByIdWithRelatedForPublicAccess"/> so anonymous callers
/// are not blocked by tenant query filters when request tenant context does not match the form's tenant.
/// </summary>
public sealed class CreateFormAccessTokenHandlerTests
{
    [Fact]
    public async Task Handle_UsesByIdWithRelatedForPublicAccess_AndSucceeds()
    {
        const long formId = 42L;
        const long formTenantId = 99L;

        IRepository<Form> formRepository = Substitute.For<IRepository<Form>>();
        IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> policy =
            Substitute.For<IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>>();
        IFormAccessTokenService tokenService = Substitute.For<IFormAccessTokenService>();

        ICachedData<PublicFormAccessData> cached = new Cached<PublicFormAccessData>(
            PublicFormAccessData.CreatePublicForm(formId),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(10),
            "etag-mint-test");
        policy.GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ICachedData<PublicFormAccessData>>(cached));

        var form = new Form(formTenantId, "Mint test");
        form.Id = formId;
        var definition = new FormDefinition(formTenantId, isDraft: false, jsonData: "{}");
        form.AddFormDefinition(definition, isActive: true);

        formRepository
            .FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdWithRelatedForPublicAccess>(), Arg.Any<CancellationToken>())
            .Returns(form);

        FormAccessTokenDto dto = new("signed-jwt", DateTime.UtcNow.AddMinutes(60));
        tokenService.CreateToken(formId, formTenantId)
            .Returns(Result.Success(dto));

        var sut = new CreateFormAccessTokenHandler(formRepository, policy, tokenService);

        var command = new CreateFormAccessTokenCommand(formId);
        Result<FormAccessTokenDto> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(dto);

        await formRepository.Received(1).FirstOrDefaultAsync(
            Arg.Any<FormSpecifications.ByIdWithRelatedForPublicAccess>(),
            Arg.Any<CancellationToken>());
        await formRepository.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<FormSpecifications.ByIdWithRelated>(),
            Arg.Any<CancellationToken>());
        tokenService.Received(1).CreateToken(formId, formTenantId);
    }
}
