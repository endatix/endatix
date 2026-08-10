using Endatix.Core.UseCases.DataLists.GetById;

namespace Endatix.Core.Tests.UseCases.DataLists.GetById;

public class GetDataListByIdQueryTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        GetDataListByIdQuery query = new(42);

        query.DataListId.Should().Be(42);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        Action act = () => _ = new GetDataListByIdQuery(dataListId);

        act.Should().Throw<ArgumentException>();
    }
}
