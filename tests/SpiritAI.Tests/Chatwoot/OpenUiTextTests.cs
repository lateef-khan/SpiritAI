using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// What staff read in Chatwoot for an AI message. The programs are answers the Spirit agent gave
/// on 2026-09-23; the expected lines are what the widget draws for them, top to bottom.
/// </summary>
public sealed class OpenUiTextTests
{
    [Fact]
    public void ACardBecomesItsTextInTheOrderTheCardShowsIt()
    {
        const string program = """
            root = Card([greet, followUps])
            greet = TextContent("Hello, friend, welcome", "large-heavy")
            followUps = FollowUpBlock([fu1])
            fu1 = FollowUpItem("I need help with my Spirit machine")
            """;

        Assert.Equal(
            "[Card shown to the customer]\nHello, friend, welcome\nI need help with my Spirit machine",
            OpenUiText.ForStaff(program));
    }

    [Fact]
    public void NamesAreFollowedWhereverTheyAreDefined()
    {
        const string program = """
            fu1 = FollowUpItem("Show me treadmills")
            root = Card([header, list, FollowUpBlock([fu1])])
            header = CardHeader("Your XT685", "Treadmill")
            list = ListBlock([ListItem("Belt", "Replace every 5 years"), ListItem("Deck", "Flip once")], "number")
            """;

        Assert.Equal(
            "[Card shown to the customer]\nYour XT685\nTreadmill\nBelt\nReplace every 5 years\nDeck\nFlip once\nShow me treadmills",
            OpenUiText.ForStaff(program));
    }

    [Fact]
    public void EscapesInAStringAreRead()
        => Assert.Equal(
            "[Card shown to the customer]\nSay \"hi\"\nthen wait",
            OpenUiText.ForStaff("root = Card([TextContent(\"Say \\\"hi\\\"\\nthen wait\")])"));

    [Fact]
    public void MarkdownGoesToStaffAsItIs()
        => Assert.Equal("**Yes** — the XT685 folds.", OpenUiText.ForStaff("**Yes** — the XT685 folds."));

    [Fact]
    public void AProgramThatCannotBeReadGoesToStaffAsItIs()
    {
        const string broken = "root = Card([TextContent(\"never closed";

        Assert.Equal(broken, OpenUiText.ForStaff(broken));
    }
}
