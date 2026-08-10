using Endatix.Core.UseCases.DataLists.Translations;

namespace Endatix.Core.Tests.UseCases.DataLists.Translations;

public class GetDataListTranslationsCsvQueryTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        GetDataListTranslationsCsvQuery query = new(42);

        query.DataListId.Should().Be(42);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        Action act = () => _ = new GetDataListTranslationsCsvQuery(dataListId);

        act.Should().Throw<ArgumentException>();
    }
}
