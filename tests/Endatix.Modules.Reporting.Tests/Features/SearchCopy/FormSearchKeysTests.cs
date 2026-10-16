using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class FormSearchKeysTests
{
    private static readonly DateTime Earlier = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Refresh_ThirtySearchableColumns_AddsThemAll()
    {
        // Arrange
        var compile = Compile(Searchable(30));

        // Act
        var refresh = FormSearchKeys.Empty.Refresh(compile, Now);

        // Assert
        refresh.Keys.Keys.Select(key => key.Key).Should().Equal(Names(30));
        refresh.Keys.Keys.Should().OnlyContain(key => key.Source == SearchKeySources.Auto && key.AddedAt == Now);
        refresh.RowsNeedRebuild.Should().BeFalse("the rows hold none of these columns yet");
    }

    [Fact]
    public void Refresh_ThreeHundredSearchableColumns_AddsTheFirstFifty()
    {
        // Arrange
        var compile = Compile(Searchable(300));

        // Act
        var refresh = FormSearchKeys.Empty.Refresh(compile, Now);

        // Assert
        refresh.Keys.Keys.Select(key => key.Key).Should().Equal(Names(FormSearchKeys.MaxAutomatic));
    }

    [Fact]
    public void Refresh_KeysAlreadyHeld_KeepsThemFirstAndAppendsNewOnes()
    {
        // Arrange
        var existing = Refreshed(FormSearchKeys.Empty, Compile(Searchable(2)), Earlier);
        var searchable = new[] { Column("q003") }.Concat(Searchable(2)).ToList();

        // Act
        var refresh = existing.Refresh(Compile(searchable), Now);

        // Assert
        refresh.Keys.Keys.Select(key => (key.Key, key.AddedAt)).Should().Equal(
            ("q001", Earlier), ("q002", Earlier), ("q003", Now));
    }

    [Fact]
    public void Refresh_AutomaticSlotsFull_AddsNoMoreAndLeavesTheUserSlotsFree()
    {
        // Arrange
        var full = Refreshed(FormSearchKeys.Empty, Compile(Searchable(50)), Earlier);
        var oneMore = Searchable(51);

        // Act
        var refresh = full.Refresh(Compile(oneMore), Now);

        // Assert
        refresh.Keys.Keys.Should().HaveCount(FormSearchKeys.MaxAutomatic);
        refresh.Keys.Keys.Should().NotContain(key => key.Key == "q051");
    }

    [Fact]
    public void Refresh_UserKeysFillTheList_AddsNoAutomaticKeyPastTheCap()
    {
        // Arrange
        var keys = Names(60).Select((name, index) =>
            new SearchKey(name, index < 45 ? SearchKeySources.Auto : SearchKeySources.User, SubmissionGridFilterTypes.Text, Earlier));
        var existing = FormSearchKeys.FromJson(System.Text.Json.JsonSerializer.Serialize(
            keys, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));

        // Act
        var refresh = existing.Refresh(Compile(Searchable(100)), Now);

        // Assert
        refresh.Keys.Keys.Should().HaveCount(FormSearchKeys.MaxKeys);
        refresh.Keys.Keys.Count(key => key.Source == SearchKeySources.Auto).Should().Be(49);
    }

    [Fact]
    public void Refresh_ColumnGoneFromTheSchema_DropsItsKey()
    {
        // Arrange
        var existing = Refreshed(FormSearchKeys.Empty, Compile(Searchable(3)), Earlier);
        var remaining = Searchable(3).Where(column => column.Key != "q002").ToList();

        // Act
        var refresh = existing.Refresh(Compile(remaining), Now);

        // Assert
        refresh.Keys.Keys.Select(key => key.Key).Should().Equal("q001", "q003");
    }

    [Fact]
    public void Refresh_KeyAddedForAColumnTheRowsAlreadyHold_AsksForARebuild()
    {
        // Arrange
        var searchable = Searchable(2);
        var compile = Compile(searchable) with { PreviousColumnKeys = Names(2).ToHashSet() };

        // Act
        var refresh = FormSearchKeys.Empty.Refresh(compile, Now);

        // Assert
        refresh.RowsNeedRebuild.Should().BeTrue();
    }

    [Fact]
    public void Refresh_FilterTypeChanged_TakesTheNewOneAndAsksForARebuild()
    {
        // Arrange
        var existing = Refreshed(FormSearchKeys.Empty, Compile(Searchable(1)), Earlier);
        var retyped = new List<SearchableColumn> { new("q001", SubmissionGridFilterTypes.Date) };

        // Act
        var refresh = existing.Refresh(Compile(retyped), Now);

        // Assert
        refresh.Keys.Keys.Should().ContainSingle().Which.FilterType.Should().Be(SubmissionGridFilterTypes.Date);
        refresh.RowsNeedRebuild.Should().BeTrue();
    }

    [Fact]
    public void ToJson_ThenFromJson_RoundTrips()
    {
        // Arrange
        var keys = Refreshed(FormSearchKeys.Empty, Compile(Searchable(2)), Now);

        // Act
        var json = keys.ToJson();
        var read = FormSearchKeys.FromJson(json);

        // Assert
        json.Should().Contain("\"key\":\"q001\"").And.Contain("\"source\":\"auto\"").And.Contain("\"filterType\":\"text\"");
        read.Keys.Should().Equal(keys.Keys);
    }

    [Fact]
    public void Refresh_KeyForAColumnTheRowsHold_StampsItWithTheNextRevision()
    {
        // Arrange
        var searchable = Searchable(2);
        SearchKeysCompile compile = new(searchable, Keys(searchable), Keys(searchable));

        // Act
        var refresh = FormSearchKeys.Empty.Refresh(compile, Now);

        // Assert
        refresh.Keys.Revision.Should().Be(1);
        refresh.Keys.Keys.Should().OnlyContain(key => key.Revision == 1);
    }

    [Fact]
    public void Refresh_KeysForNewColumnsOnly_KeepsTheRevision()
    {
        // Arrange
        var existing = WithUserKeys(Refreshed(FormSearchKeys.Empty, Compile(Searchable(2)), Earlier), "q002");
        var searchable = Searchable(3);
        SearchKeysCompile compile = new(searchable, Keys(searchable), Keys(Searchable(2)));

        // Act
        var refresh = existing.Refresh(compile, Now);

        // Assert
        refresh.Keys.Revision.Should().Be(existing.Revision);
        refresh.Keys.Find("q003")!.Revision.Should().Be(existing.Revision);
    }

    [Fact]
    public void AddUser_ThirtyAutomaticKeys_AcceptsFourteenUserKeys()
    {
        // Arrange
        var automatic = Refreshed(FormSearchKeys.Empty, Compile(Searchable(30)), Earlier);

        // Act
        var withUserKeys = WithUserKeys(automatic, UserNames(FormSearchKeys.MaxUserKeys));

        // Assert
        withUserKeys.Keys.Should().HaveCount(44);
        withUserKeys.UserKeyCount.Should().Be(14);
    }

    [Fact]
    public void AddUser_FifteenthUserKey_IsInvalidAndNamesTheCap()
    {
        // Arrange
        var full = WithUserKeys(Refreshed(FormSearchKeys.Empty, Compile(Searchable(30)), Earlier), UserNames(14));

        // Act
        var result = full.AddUser(Column("u015"), Now);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("14").And.Contain("64");
    }

    [Fact]
    public void AddUser_FreeSlot_AppendsAUserKeyAtTheNextRevision()
    {
        // Arrange
        var automatic = Refreshed(FormSearchKeys.Empty, Compile(Searchable(2)), Earlier);

        // Act
        var added = automatic.AddUser(new SearchableColumn("visit", SubmissionGridFilterTypes.Date), Now).Value;

        // Assert
        added.Revision.Should().Be(automatic.Revision + 1);
        added.Keys[^1].Should().Be(new SearchKey("visit", SearchKeySources.User, SubmissionGridFilterTypes.Date, Now, added.Revision));
    }

    [Fact]
    public void AddUser_KeyAlreadySearchable_ReturnsTheSameKeys()
    {
        // Arrange
        var automatic = Refreshed(FormSearchKeys.Empty, Compile(Searchable(2)), Earlier);

        // Act
        var result = automatic.AddUser(Column("q001"), Now);

        // Assert
        result.Value.Should().BeSameAs(automatic);
    }

    [Fact]
    public void RemoveUser_UserKey_DropsItAndKeepsTheRevision()
    {
        // Arrange
        var keys = WithUserKeys(Refreshed(FormSearchKeys.Empty, Compile(Searchable(1)), Earlier), "u001", "u002");

        // Act
        var removed = keys.RemoveUser("u001").Value;

        // Assert
        removed.Keys.Select(key => key.Key).Should().Equal("q001", "u002");
        removed.Revision.Should().Be(keys.Revision);
    }

    [Fact]
    public void RemoveUser_AutomaticKey_IsInvalid()
    {
        // Arrange
        var keys = Refreshed(FormSearchKeys.Empty, Compile(Searchable(1)), Earlier);

        // Act
        var result = keys.RemoveUser("q001");

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void RemoveUser_KeyNotSearchable_IsNotFound()
    {
        // Act
        var result = FormSearchKeys.Empty.RemoveUser("q001");

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public void RevisionNeededFor_SeveralKeys_IsTheNewestOfTheirRevisions()
    {
        // Arrange
        var keys = WithUserKeys(Refreshed(FormSearchKeys.Empty, Compile(Searchable(1)), Earlier), "u001", "u002");

        // Act
        var needed = keys.RevisionNeededFor(["q001", "u001", "not-a-key"]);

        // Assert
        needed.Should().Be(keys.Find("u001")!.Revision);
    }

    [Fact]
    public void RevisionNeededFor_NoSearchKey_IsZero()
    {
        // Act
        var needed = FormSearchKeys.Empty.RevisionNeededFor(["q001"]);

        // Assert
        needed.Should().Be(0);
    }

    private static FormSearchKeys WithUserKeys(FormSearchKeys keys, params string[] names) =>
        names.Aggregate(keys, (current, name) => current.AddUser(Column(name), Now).Value);

    private static string[] UserNames(int count) =>
        Enumerable.Range(1, count).Select(index => $"u{index:D3}").ToArray();

    private static HashSet<string> Keys(IEnumerable<SearchableColumn> columns) =>
        columns.Select(column => column.Key).ToHashSet();

    private static FormSearchKeys Refreshed(FormSearchKeys keys, SearchKeysCompile compile, DateTime at) =>
        keys.Refresh(compile, at).Keys;

    private static SearchKeysCompile Compile(IReadOnlyList<SearchableColumn> searchable) =>
        new(searchable, searchable.Select(column => column.Key).ToHashSet(), new HashSet<string>());

    private static List<SearchableColumn> Searchable(int count) => Names(count).Select(Column).ToList();

    private static SearchableColumn Column(string key) => new(key, SubmissionGridFilterTypes.Text);

    private static List<string> Names(int count) =>
        Enumerable.Range(1, count).Select(index => $"q{index:D3}").ToList();
}
