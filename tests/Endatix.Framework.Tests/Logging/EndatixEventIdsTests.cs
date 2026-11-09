using System.Reflection;
using Endatix.Framework.Logging;

namespace Endatix.Framework.Tests.Logging;

public class EndatixEventIdsTests
{
    private const string RangeStartField = "RangeStart";
    private const string RangeEndField = "RangeEnd";
    private const string AllField = "All";

    private sealed record EventIdGroup(string Name, int RangeStart, int RangeEnd, int[] All, IReadOnlyList<int> Declared);

    private static IReadOnlyList<EventIdGroup> Groups() =>
        typeof(EndatixEventIds)
            .GetNestedTypes(BindingFlags.Public)
            .Where(type => type != typeof(EndatixEventIds.Ranges))
            .Select(ToGroup)
            .ToList();

    private static EventIdGroup ToGroup(Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        int Constant(string name) => (int)fields.Single(field => field.Name == name).GetRawConstantValue()!;

        var declared = fields
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .Where(field => field.Name is not RangeStartField and not RangeEndField)
            .Select(field => (int)field.GetRawConstantValue()!)
            .ToList();
        var all = (int[])fields.Single(field => field.Name == AllField).GetValue(null)!;

        return new EventIdGroup(type.Name, Constant(RangeStartField), Constant(RangeEndField), all, declared);
    }

    [Fact]
    public void Groups_IncludeEveryArea()
    {
        // Act
        var names = Groups().Select(group => group.Name);

        // Assert
        names.Should().Contain([
            nameof(EndatixEventIds.Lifecycle),
            nameof(EndatixEventIds.Hosting),
            nameof(EndatixEventIds.ReCaptcha),
            nameof(EndatixEventIds.Webhooks),
            nameof(EndatixEventIds.Email),
            nameof(EndatixEventIds.Api)
        ]);
    }

    [Fact]
    public void All_EveryGroup_ListsEachDeclaredEventIdOnce()
    {
        foreach (var group in Groups())
        {
            group.All.Should().OnlyHaveUniqueItems($"{group.Name}.All must not repeat an EventId");
            group.All.Should().BeEquivalentTo(group.Declared, $"{group.Name}.All must list every EventId the group declares");
        }
    }

    [Fact]
    public void All_EveryGroup_StaysInsideItsRange()
    {
        foreach (var group in Groups())
        {
            group.All.Should().OnlyContain(
                eventId => EndatixEventIds.IsInRange(eventId, group.RangeStart, group.RangeEnd),
                $"{group.Name} owns {group.RangeStart}-{group.RangeEnd}");
        }
    }

    [Fact]
    public void Ranges_NoTwoGroups_Overlap()
    {
        // Arrange
        var ordered = Groups().OrderBy(group => group.RangeStart).ToList();

        // Act
        var overlaps = ordered
            .Zip(ordered.Skip(1))
            .Where(pair => pair.Second.RangeStart <= pair.First.RangeEnd)
            .Select(pair => $"{pair.First.Name} and {pair.Second.Name}")
            .ToList();

        // Assert
        overlaps.Should().BeEmpty();
    }

    [Fact]
    public void AllStartup_ContainsOnlyStartupGroups()
    {
        // Arrange
        int[] startup =
        [
            .. EndatixEventIds.Lifecycle.All,
            .. EndatixEventIds.Migrations.All,
            .. EndatixEventIds.Seeding.All,
            .. EndatixEventIds.IdentitySeed.All,
            .. EndatixEventIds.Hosting.All
        ];

        // Assert
        EndatixEventIds.AllStartup.Should().Equal(startup);
        EndatixEventIds.AllStartup.Should().NotIntersectWith(EndatixEventIds.Webhooks.All)
            .And.NotIntersectWith(EndatixEventIds.Email.All)
            .And.NotIntersectWith(EndatixEventIds.Api.All);
    }
}
