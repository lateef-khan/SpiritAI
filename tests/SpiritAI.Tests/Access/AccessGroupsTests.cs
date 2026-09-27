using Xunit;

using SpiritAI.Access;

namespace SpiritAI.Tests.Access;

/// <summary>The ladder of roles spec section 3: many groups, the highest one picks the entry.</summary>
public sealed class AccessGroupsTests
{
    [Theory]
    [InlineData(AccessGroup.TechService, AccessGroup.TechServiceManager)]
    [InlineData(AccessGroup.TechServiceManager, AccessGroup.TechService)]
    public void TechServiceAndItsManager_RunTheManagerEntry(AccessGroup first, AccessGroup second)
    {
        var highest = AccessGroups.Highest([first, second]);

        Assert.Equal(AccessGroup.TechServiceManager, highest);
        Assert.Equal("manager", AccessGroups.EntryOf(highest!.Value));
    }

    [Fact]
    public void NoGroups_HaveNoHighest()
    {
        Assert.Null(AccessGroups.Highest([]));
    }

    [Theory]
    [InlineData(AccessGroup.Guest, "main")]
    [InlineData(AccessGroup.Dealer, "dealer")]
    [InlineData(AccessGroup.InsideSalesSupervisor, "staff")]
    [InlineData(AccessGroup.InsideSalesManager, "manager")]
    [InlineData(AccessGroup.Admin, "admin")]
    public void EachGroup_RunsTheEntryTheSpecNames(AccessGroup group, string entry)
    {
        Assert.Equal(entry, AccessGroups.EntryOf(group));
    }
}
