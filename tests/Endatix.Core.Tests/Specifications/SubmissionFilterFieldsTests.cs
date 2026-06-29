using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Tests.Specifications;

public class SubmissionFilterFieldsTests
{
    [Fact]
    public void SubmissionsByFormIdSpecs_WithSubmitterProfileFilter_DoNotBuildGenericPropertyFilter()
    {
        FilterParameters filterParams = new(["submitterProfile.email:test@example.com"]);

        Action createListSpec = () => _ = new SubmissionsByFormIdSpec(1, new PagingParameters(1, 10), filterParams);
        Action createCountSpec = () => _ = new SubmissionsByFormIdCountSpec(1, filterParams);

        createListSpec.Should().NotThrow();
        createCountSpec.Should().NotThrow();
    }

    [Fact]
    public void SelectSubmitterProfileFilters_ReturnsOnlyProfileFilters()
    {
        FilterParameters filterParams = new([
            "submitterProfile.email:test@example.com",
            "submitterDisplayId:panelist-1"
        ]);

        var filters = SubmissionFilterFields.SelectSubmitterProfileFilters(filterParams.Criteria);

        filters.Should().ContainSingle();
        filters[0].Field.Should().Be("submitterProfile.email");
    }
}
