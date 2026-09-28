using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The name a member of staff goes by in front of a visitor. Chatwoot's <c>available_name</c> is
/// the display name when one is set and the account name when not
/// (<c>UserAttributeHelpers#available_name</c>, 4.18.0).
/// </summary>
public sealed class ChatwootStaffNameTests
{
    [Theory]
    [InlineData("Matthew Hsu", "Matthew Hsu", "Matthew")]
    [InlineData("Matthew Hsu", "Matt", "Matt")]
    [InlineData("Matthew Hsu", null, "Matthew")]
    [InlineData("  Matthew   Hsu ", "  Matthew   Hsu ", "Matthew")]
    [InlineData("Dana", "Dana", "Dana")]
    [InlineData("", "", null)]
    [InlineData(null, null, null)]
    public void ADisplayNameWinsElseTheFirstWordOfTheName(string? name, string? availableName, string? expected)
    {
        Assert.Equal(expected, ChatwootStaffName.Of(name, availableName));
    }
}
