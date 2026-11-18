using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public sealed class SubmissionWithDefinitionAndFormSpecTests
{
    [Fact]
    public void Constructor_Default_DoesNotLoadTheTheme()
    {
        var spec = new SubmissionWithDefinitionAndFormSpec(formId: 1, submissionId: 2);

        LoadsTheme(spec).Should().BeFalse();
    }

    [Fact]
    public void Constructor_IncludeTheme_LoadsTheFormTheme()
    {
        var spec = new SubmissionWithDefinitionAndFormSpec(formId: 1, submissionId: 2, includeTheme: true);

        LoadsTheme(spec).Should().BeTrue();
    }

    /// <summary>Whether the spec includes <c>Form.Theme</c>.</summary>
    internal static bool LoadsTheme(SubmissionWithDefinitionAndFormSpec spec) =>
        spec.IncludeExpressions.Any(include =>
            include.Type == IncludeTypeEnum.ThenInclude
            && include.LambdaExpression.Body is MemberExpression { Member.Name: nameof(Form.Theme) });
}
