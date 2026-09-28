using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// What staff read in Chatwoot for an AI message. The expected Markdown follows the mapping in
/// docs/superpowers/specs/2026-09-23-openui-to-markdown-design.md.
/// </summary>
public sealed class OpenUiMarkdownTests
{
    [Fact]
    public void TheWorkedExampleInTheSpecBecomesItsMarkdown()
    {
        const string program = """
            root = Card([h, tbl, list, fu])
            h = CardHeader("Treadmill F80", "Our best seller")
            tbl = Table([Col("Spec", ["Motor","Deck"]), Col("Value", ["4 HP","22 x 60 in"])])
            list = ListBlock([ListItem("Manual", "PDF", null, "Open", Action([@OpenUrl("https://x.com/m.pdf")]))])
            fu = FollowUpBlock([FollowUpItem("Compare with F85")])
            """;

        Assert.Equal(
            """
            ### Treadmill F80
            _Our best seller_

            | Spec | Value |
            | --- | --- |
            | Motor | 4 HP |
            | Deck | 22 x 60 in |

            - **[Manual](https://x.com/m.pdf)** — PDF

            _Suggested replies:_
            - Compare with F85
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void HeavyTextOnOneLineIsBold()
    {
        const string program = """
            root = Card([greet, followUps])
            greet = TextContent("Hello, friend, welcome", "large-heavy")
            followUps = FollowUpBlock([fu1])
            fu1 = FollowUpItem("I need help with my Spirit machine")
            """;

        Assert.Equal(
            """
            **Hello, friend, welcome**

            _Suggested replies:_
            - I need help with my Spirit machine
            """,
            OpenUiMarkdown.ForStaff(program));
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
            """
            ### Your XT685
            _Treadmill_

            1. **Belt** — Replace every 5 years
            2. **Deck** — Flip once

            _Suggested replies:_
            - Show me treadmills
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void TextContentIsAlreadyMarkdownAndItsEscapesAreRead()
        => Assert.Equal(
            "Say \"hi\"\nthen **wait**",
            OpenUiMarkdown.ForStaff("root = Card([TextContent(\"Say \\\"hi\\\"\\nthen **wait**\")])"));

    [Fact]
    public void ACalloutIsAQuoteLabelledByItsVariant()
        => Assert.Equal(
            """
            > **Warning: Unplug first**
            > Turn off the power before service.
            """,
            OpenUiMarkdown.ForStaff("""root = Card([Callout("warning", "Unplug first", "Turn off the power before service.")])"""));

    [Fact]
    public void ACodeFenceIsLongerThanAnyBacktickRunInTheCode()
        => Assert.Equal(
            """
            ````text
            use ``` to fence
            ````
            """,
            OpenUiMarkdown.ForStaff("""root = Card([CodeBlock("text", "use ``` to fence")])"""));

    [Fact]
    public void AChartBecomesATableOfItsNumbers()
    {
        const string program = """
            root = Card([chart])
            chart = BarChart(["Jan", "Feb"], [Series("Sales", [10, 20]), Series("Returns", [1, 2.5])], "grouped", "Month")
            """;

        Assert.Equal(
            """
            _Bar chart_

            | Month | Sales | Returns |
            | --- | --- | --- |
            | Jan | 10 | 1 |
            | Feb | 20 | 2.5 |
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void APieChartReadsPluckedArrays()
    {
        const string program = """
            root = Card([PieChart(data.categories, data.values)])
            data = {categories: ["Treadmills", "Bikes"], values: [60, 40]}
            """;

        Assert.Equal(
            """
            _Pie chart_

            | Label | Value |
            | --- | --- |
            | Treadmills | 60 |
            | Bikes | 40 |
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void ATableCellEscapesPipesAndShowsTheTextOfAComponent()
        => Assert.Equal(
            """
            | Part | Stock |
            | --- | --- |
            | Belt \| 60 in | In stock |
            """,
            OpenUiMarkdown.ForStaff("""root = Card([Table([Col("Part", ["Belt | 60 in"]), Col("Stock", [Tag("In stock", null, "sm", "success")])])])"""));

    [Fact]
    public void AButtonThatOpensAPageIsALink()
    {
        const string program = """
            root = Card([Buttons([manual, Button("Talk to us")])])
            manual = Button("Manual", Action([@OpenUrl("https://spiritfitness.com/m.pdf")]))
            """;

        Assert.Equal(
            "_Buttons:_ [Manual](https://spiritfitness.com/m.pdf) · `Talk to us`",
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void AFormListsItsFieldsThenItsButtons()
    {
        const string program = """
            root = Card([form])
            form = Form("contact", btns, [email, size])
            email = FormControl("Email", Input("email", "you@example.com", "email", { required: true, email: true }))
            size = FormControl("Size", Select("size", [SelectItem("s", "Small"), SelectItem("l", "Large")]))
            btns = Buttons([Button("Submit", Action([@ToAssistant("Submit")]), "primary")])
            """;

        Assert.Equal(
            """
            _Form:_
            - **Email**: email
            - **Size**: Small / Large

            _Buttons:_ `Submit`
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void StepsAreANumberedList()
        => Assert.Equal(
            """
            1. **Unplug** — Wait five minutes
            2. **Lift the hood** — Two screws
            """,
            OpenUiMarkdown.ForStaff("""root = Card([Steps([StepsItem("Unplug", "Wait five minutes"), StepsItem("Lift the hood", "Two screws")])])"""));

    [Fact]
    public void EachTabIsAHeadingOverItsContent()
    {
        const string program = """
            root = Card([Tabs([TabItem("a", "Specs", [TextContent("4 HP")]), TabItem("b", "Warranty", [TextContent("Lifetime frame")])])])
            """;

        Assert.Equal(
            """
            #### Specs

            4 HP

            #### Warranty

            Lifetime frame
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void CarouselSlidesAreSplitByARule()
    {
        const string program = """
            root = Card([Carousel([[TextContent("XT685"), ImageBlock("https://x.com/a.png", "The XT685")], [TextContent("XT485")]], "card")])
            """;

        Assert.Equal(
            """
            XT685

            ![The XT685](https://x.com/a.png)

            ---

            XT485
            """,
            OpenUiMarkdown.ForStaff(program));
    }

    [Fact]
    public void AGalleryShowsEachImageWithItsDetails()
        => Assert.Equal(
            """
            ![Front](https://x.com/f.png)
            _Belt and deck_
            """,
            OpenUiMarkdown.ForStaff("""root = Card([ImageGallery([{src: "https://x.com/f.png", alt: "Front", details: "Belt and deck"}])])"""));

    [Fact]
    public void TagsAreCodeSpans()
        => Assert.Equal("`Folding` `Bluetooth`", OpenUiMarkdown.ForStaff("""root = Card([TagBlock(["Folding", "Bluetooth"])])"""));

    [Fact]
    public void AnUnknownComponentShowsItsWords()
        => Assert.Equal("Hello there", OpenUiMarkdown.ForStaff("""root = Card([Banner("Hello there", "info")])"""));

    [Fact]
    public void MarkdownGoesToStaffAsItIs()
        => Assert.Equal("**Yes** — the XT685 folds.", OpenUiMarkdown.ForStaff("**Yes** — the XT685 folds."));

    [Fact]
    public void AProgramThatCannotBeReadGoesToStaffAsItIs()
    {
        const string broken = "root = Card([TextContent(\"never closed";

        Assert.Equal(broken, OpenUiMarkdown.ForStaff(broken));
    }

    [Fact]
    public void ACardWithNoWordsGoesToStaffAsItIs()
    {
        const string empty = "root = Card([])";

        Assert.Equal(empty, OpenUiMarkdown.ForStaff(empty));
    }

    [Fact]
    public void AProgramThatHoldsItselfStillEnds()
    {
        const string loop = """
            root = Card([a])
            a = Stack([a])
            """;

        Assert.Equal(loop, OpenUiMarkdown.ForStaff(loop));
    }
}
